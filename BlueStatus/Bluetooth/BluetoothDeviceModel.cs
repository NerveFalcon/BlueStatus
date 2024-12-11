#region

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

#endregion

namespace BlueStatus.Bluetooth;

public class BluetoothDeviceModel
{
	private BluetoothDeviceModel(BluetoothDeviceInfo bluetoothDeviceInfo, BluetoothDevice bluetoothDevice)
	{
		DeviceInfo = bluetoothDeviceInfo;
		BluetoothDevice = bluetoothDevice;
		Services = [];
	}

	public BluetoothAddress DeviceAddress => DeviceInfo.DeviceAddress;
	public ClassOfDevice ClassOfDevice => DeviceInfo.ClassOfDevice;
	public string DeviceName => DeviceInfo.DeviceName;
	public bool Connected => DeviceInfo.Connected;

	public Dictionary<string, RfcommDeviceService> Services { get; private set; }
	public BluetoothDevice BluetoothDevice { get; }
	public BluetoothDeviceInfo DeviceInfo { get; }

	public static async Task<BluetoothDeviceModel> FromDevice(BluetoothDeviceInfo deviceInfo)
	{
		var bluDevice = await BluetoothDevice.FromBluetoothAddressAsync(deviceInfo.DeviceAddress);
		var device = new BluetoothDeviceModel(deviceInfo, bluDevice);
		await LoadServices(device);

		return device;
	}

	private static async Task LoadServices(BluetoothDeviceModel model)
	{
		var serviceRes = await model.BluetoothDevice.GetRfcommServicesAsync();
		if (serviceRes.Error != BluetoothError.Success)
			return;

		var i = 0;
		model.Services =
			serviceRes.Services.ToDictionary(k => BluetoothService.GetName(k.ServiceId.Uuid) ?? $"Undefined{++i}",
				v => v);
	}
}