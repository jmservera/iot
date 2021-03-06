﻿using DhtReadService;
using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace DhtView
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            InitializeSensor();
        }
        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush orangeBrush = new SolidColorBrush(Windows.UI.Colors.Orange);
        const string iotHubUri = "sqlsathub.azure-devices.net";
        const string deviceKey = "WznTpvxyTJcme0dSUYdGNH/KneVXxztbnvlcAuFEKFc=";
        const string rpiName = "sqlsat_01";

        private async void InitializeSensor()
        {
            var key = AuthenticationMethodFactory.CreateAuthenticationWithRegistrySymmetricKey(rpiName, deviceKey);
            DeviceClient deviceClient = DeviceClient.Create(iotHubUri, key, TransportType.Http1);

            Task ts = SendEvents(deviceClient);
            Task tr = ReceiveCommands(deviceClient);

            await Task.WhenAll(ts, tr);
        }

        private async Task ReceiveCommands(DeviceClient deviceClient)
        {
            System.Diagnostics.Debug.WriteLine("\nDevice waiting for commands from IoTHub...\n");
            Message receivedMessage;
            string messageData;
            int recoverTimeout = 1000;
            while (true)
            {
                try
                {
                    receivedMessage = await deviceClient.ReceiveAsync();// TimeSpan.FromSeconds(1));

                    if (receivedMessage != null)
                    {
                        messageData = Encoding.ASCII.GetString(receivedMessage.GetBytes());
                        System.Diagnostics.Debug.WriteLine(String.Format("\t{0}> Received message: {1}", DateTime.Now.ToLocalTime(), messageData));
                        messages.Text = $"{DateTime.Now.ToLocalTime()}> Received message: {messageData}";
                        await deviceClient.CompleteAsync(receivedMessage);
                    }
                    recoverTimeout = 1000;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    await Task.Delay(recoverTimeout);
                    recoverTimeout *= 10; // increment timeout for connection recovery
                    if (recoverTimeout > 600000)//set a maximum timeout
                    {
                        recoverTimeout = 600000;
                    }
                }
            }

        }

        const string GUID = "58980B74-8117-464A-A9FA-1850B0E2F0B3"; //todo create a unique guid per device
        const string ORGANIZATION = "Microsoft";
        const string DISPLAYNAME = "Raspberry Pi 2 DHT22";
        const string LOCATION = "Madrid"; //todo config the location
        const string TEMPMEASURE = "Temperature";
        const string HUMIDMEASURE = "Humidity";
        const string TEMPUNITS = "C";
        const string HUMIDUNITS = "%";
        const string jsonFormat = "{{\"guid\":\"{0}\", \"organization\":\"{1}\", \"displayname\": \"{2}\", \"location\": \"{3}\", \"measurename\": \"{4}\", \"unitofmeasure\": \"{5}\", \"value\":{6}, \"timecreated\":\"{7:u}\" }}";


        private async Task SendEvents(DeviceClient deviceClient)
        {
            try
            {
                string dataBuffer;
                SensorReader c = new SensorReader(4);
                Guid id = Guid.NewGuid();
                while (true)
                {
                    var data = c.Read();
                    if (data != null && data.IsValid)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("Temp:{0} Hum:{1}", data.Temperature, data.Humidity));
                            refreshData(data);
                            dataBuffer = string.Format(jsonFormat,
                                GUID, ORGANIZATION, DISPLAYNAME, LOCATION, TEMPMEASURE, TEMPUNITS, data.Temperature, DateTime.UtcNow);
                            Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                            await deviceClient.SendEventAsync(eventMessage);
                            dataBuffer = string.Format(jsonFormat,
                                GUID, ORGANIZATION, DISPLAYNAME, LOCATION, HUMIDMEASURE, HUMIDUNITS, data.Humidity, DateTime.UtcNow);
                            eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                            await deviceClient.SendEventAsync(eventMessage);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                        }
                    }
                    await Task.Delay(2000).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0}: {1}", ex.Message, ex.StackTrace));
            }
        }

        private async void refreshData(Dht11Reading data)
        {
            {
                await textBlockTemp.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (data.IsValid)
                    {
                        this.textBlockTemp.Text = data.Temperature + "°C";
                        this.textBlockHum.Text = data.Humidity.ToString() + "%";
                        this.reading.Text = "";
                        if (data.Temperature >= 30)
                        {
                            rectangleTempDown.Fill = redBrush;
                        }
                        else
                        {
                            rectangleTempDown.Fill = orangeBrush;
                        }
                        if (data.Humidity >= 50)
                        {
                            rectangleHumDown.Fill = redBrush;
                        }
                        else
                        {
                            rectangleHumDown.Fill = orangeBrush;
                        }
                        rectangleTempUp.Height = 2 * (100 - data.Temperature);
                        rectangleTempDown.Height = 2 * data.Temperature;
                        rectangleHumUp.Height = 2 * (100 - data.Humidity);
                        rectangleHumDown.Height = 2 * data.Humidity;

                    }
                });

            }
        }
    }
}
