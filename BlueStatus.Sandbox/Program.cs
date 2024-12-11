// See https://aka.ms/new-console-template for more information

#region

using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using InTheHand.Net.Bluetooth;
using Buffer = Windows.Storage.Streams.Buffer;

#endregion

Console.WriteLine("Hello, World!");

Console.WriteLine("Available paired devices..");
var devices = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));

if (devices.Count == 0)
{
	Console.WriteLine("No devices found");
	Exit();
}

for (var i = 0; i < devices.Count; i++) Console.WriteLine("{0}. {1}\t{2}", i, devices[i].Name, devices[i].Id);

Console.WriteLine("Select a device to connect..");
var selectedDevice = int.Parse(Console.ReadLine());

var blDevice = await BluetoothDevice.FromIdAsync(devices[selectedDevice].Id);

if (blDevice == null) Exit();

Console.WriteLine("Discovering Rfcomm services..");
var rfcommResult = await blDevice.GetRfcommServicesAsync();
if (rfcommResult.Services.Count == 0)
{
	Console.WriteLine("No services found");
	Exit();
}

for (var i = 0; i < rfcommResult.Services.Count; i++)
	Console.WriteLine("{0}. {1} ({2})", i, rfcommResult.Services[i].ServiceId.Uuid,
		BluetoothService.GetName(rfcommResult.Services[i].ServiceId.Uuid));
Console.WriteLine("Select a service to connect..");
var selectedService = int.Parse(Console.ReadLine());

try
{
	var socket = new StreamSocket();
	await socket.ConnectAsync(rfcommResult.Services[selectedService].ConnectionHostName,
		rfcommResult.Services[selectedService].ConnectionServiceName);
	Console.WriteLine("Connected to service: " + rfcommResult.Services[selectedService].ServiceId.Uuid);

	var source = new CancellationTokenSource();

	async Task ComChatAsync(StreamSocket inSocket, CancellationTokenSource unSource)
	{
		while (true)
		{
			unSource = new(TimeSpan.FromSeconds(2));
			await ReadWrite.Read(inSocket, unSource);

			var request = GetRequest();
			switch (request)
			{
				case null:
					unSource = new(TimeSpan.FromSeconds(2));
					await ReadWrite.Read(inSocket, unSource);
					break;
				case "q":
					return;
			} 
			
			await ReadWrite.Write(inSocket, request);
			await ReadWrite.Write(inSocket, "OK");
		}
	}

	await ComChatAsync(socket, source);

	Console.ReadKey();
}
catch (Exception e)
{
	Console.WriteLine("Could not connect to service " + e.Message);
	Exit();
}

static void Exit()
{
	Console.ReadKey();
	Environment.Exit(1);
}

string? GetRequest()
{
	Console.Write("__:");
	return Console.ReadLine();
}

internal static class ReadWrite
{
	public static async Task Read(StreamSocket socket, CancellationTokenSource source)
	{
		IBuffer buffer = new Buffer(1024);
		IBuffer result;
		uint bytesRead = 1024;

		try
		{
			var progress = socket.InputStream.ReadAsync(buffer, bytesRead, InputStreamOptions.Partial);
			result = await progress.AsTask(source.Token);
		}
		catch (TaskCanceledException)
		{
			Console.WriteLine("Canceled");
			return;
		}

		var reader = DataReader.FromBuffer(result);
		var output = reader.ReadString(result.Length);

		if (output.Length != 0)
		{
			Console.WriteLine("Received :" + output.Replace("\r", " "));
			await AutoHandle(socket, output, source);
		}
	}

	private static async Task AutoHandle(StreamSocket socket, string output, CancellationTokenSource source)
	{
		if (output.Contains("BRSF"))
		{
			await Write(socket, "+BRSF:20");
			await Write(socket, "OK");
			return;
		}

		if (output.Contains("CIND="))
		{
			await Write(socket, "+CIND: (\"service\",(0,1)),(\"call\",(0,1))");
			await Write(socket, "OK");
			return;
		}

		if (output.Contains("CIND?"))
		{
			await Write(socket, "+CIND: 1,0");
			await Write(socket, "OK");
			return;
		}
		
		if (output.Contains("CMER"))
		{
			await Write(socket, "OK");
			return;
		}

		if (output.Contains("CHLD=?"))
		{
			await Write(socket, "+CHLD: 0");
			await Write(socket, "OK");
			return;
		}
		
		if (output.Contains("IPHONEACCEV"))
			try
			{
				var batteryCmd = output.Substring(output.IndexOf("IPHONEACCEV"));
				Console.WriteLine("Battery level :" +
				                  (int.Parse(batteryCmd.Substring(batteryCmd.LastIndexOf(",") + 1)) + 1) * 10);
				source.Cancel();
			}
			catch (Exception e)
			{
				Console.WriteLine("Could not retrieve " + e.Message);
			}
	}

	public static async Task Write(StreamSocket socket, string str)
	{
		var bytesWrite = CryptographicBuffer.ConvertStringToBinary("\r\n" + str + "\r\n", BinaryStringEncoding.Utf8);
		await socket.OutputStream.WriteAsync(bytesWrite);
		Console.WriteLine(str);
	}
}