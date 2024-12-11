#region

using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using BlueStatus.Bluetooth;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

#endregion

namespace BlueStatus;

public class BluetoothManager(ILogger<BluetoothManager> logger)
{
	private BluetoothClient Client { get; } = new();

	public async Task TryConnect(BluetoothDeviceModel device, CancellationToken token)
	{
		logger.LogInformation("device connected: {Status}", device.Connected);

		try
		{
			await Client.ConnectAsync(device.DeviceAddress, BluetoothService.Handsfree);
		}
		catch (ArgumentOutOfRangeException e)
		{
			logger.LogError(e, "Device was not found");
			// todo: Return error status
			return;
		}
		catch (COMException e)
		{
			logger.LogWarning(e, "Device is busy");
			// todo: Return error status
			return;
		}

		Client.Close();
		token.ThrowIfCancellationRequested();

		await Client.ConnectAsync(device.DeviceAddress, BluetoothService.Handsfree);
		if (Client.Connected)
			logger.LogInformation("Connected");
		if (token.IsCancellationRequested)
		{
			Client.Close();
			token.ThrowIfCancellationRequested();
		}

		await using (var stream = Client.GetStream())
		{
			await CommunicateWithDevice(stream, token);
		}
		Client.Close();
		logger.LogInformation("Close connection");
	}

	private async Task CommunicateWithDevice(NetworkStream stream, CancellationToken token)
	{
		if (!stream.CanRead)
		{
			logger.LogWarning("Stream is not readable");
			// todo: return status 
			return;
		}

		var buff = new byte[1024];
		var str = new StringBuilder();
		var i = 0;
		while (i++ < 2)
		{
			var mem = new Memory<byte>(buff);
			var bytesRead = await stream.ReadAsync(mem, token);
			var response = Encoding.ASCII.GetString(buff, 0, bytesRead);
			str.Append(response);

			byte[] commandBytes;
			if (i == 1)
			{
				commandBytes = Encoding.ASCII.GetBytes("\r\n+BRSF: 871\r\n");
				await stream.WriteAsync(commandBytes, token);
			}

			commandBytes = Encoding.ASCII.GetBytes("OK\r\n");
			await stream.WriteAsync(commandBytes, token);
		}

		logger.LogInformation("{Msg}", str.ToString());
	}

	public async IAsyncEnumerable<BluetoothDeviceModel> SearchDevicesAsync(CancellationToken token)
	{
		var devicesAsync = Client.DiscoverDevicesAsync(token);
		await Task.Delay(1, token);

		await foreach (var device in devicesAsync)
		{
			yield return await BluetoothDeviceModel.FromDevice(device);
			logger.LogInformation("Find {DeviceName}", device.DeviceName);
		}
	}

	public async IAsyncEnumerable<BluetoothDeviceModel> GetPairedDevicesAsync()
	{
		foreach (var model in Client.PairedDevices.OrderByDescending(d => d.Connected))
			yield return await BluetoothDeviceModel.FromDevice(model);
	}
}