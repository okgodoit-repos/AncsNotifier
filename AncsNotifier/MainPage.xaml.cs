using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using NotificationsExtensions.Toasts;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AncsNotifier
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public Advertiser Advertiser { get; set; }
        public AncsManager AncsManager { get; set; }
        public ObservableCollection<PlainNotification> DataList = new ObservableCollection<PlainNotification>();

        public MainPage()
        {
            this.InitializeComponent();

            listView.ItemsSource = DataList;

            this.Advertiser = new Advertiser();
            this.AncsManager = new AncsManager();

            this.Advertiser.StatusChanged += OnAdvertiserStatusChanged;

            this.AncsManager.OnNotification += AncsManagerOnOnNotification;
            this.AncsManager.OnStatusChange += AncsManagerOnOnStatusChange;
            this.AncsManager.ConnectionStatusChanged += OnAncsManagerConnectionStatusChanged;
        }

        private async void AncsManagerOnOnStatusChange(string s)
        {
            setStatus(s);

            if (s == "Connected")
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    button.IsEnabled = false;
                });
            }

            if (s == "Disconnected")
            {
                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    button.IsEnabled = true;
                });
            }
        }

        private async void AncsManagerOnOnNotification(PlainNotification o)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                this.DataList.Add(o);
            });


            Show(new ToastContent()
            {
                Visual = new ToastVisual()
                {
                    TitleText = new ToastText() { Text = o.Title },
                    BodyTextLine1 = new ToastText() { Text = o.Message }
                },

                Scenario = ToastScenario.Default,

                Actions = new ToastActionsCustom()
                {
                    Buttons =
                    {
                       new ToastButtonDismiss("Ok")
                    }
                }
            });
        }


        private async void button_Click(object sender, RoutedEventArgs e)
        {
            setStatus("Service solicitation...");

            try
            {
                this.Advertiser.Start();
            }
            catch (Exception ex)
            {
                setStatus(ex.Message);
            }

            //await Task.Delay(TimeSpan.FromSeconds(2));

            //setStatus("Connecting...");

            //this.AncsManager.Connect();

            //setStatus("Waiting for device...");
        }



        private async void setStatus(string status)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                txtStatus.Text = status;
            });
        }

        private void Show(ToastContent content)
        {
            try
            {
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(content.GetXml()));

            }
            catch (Exception ex)
            {
                //yolo
            }
        }

        private void ButtonPositive_OnClick(object sender, RoutedEventArgs e)
        {
            var not = (PlainNotification)((Button)sender).DataContext;

            this.AncsManager.OnAction(not, true);
        }

        private void ButtonNegative_OnClick(object sender, RoutedEventArgs e)
        {
            var not = (PlainNotification)((Button)sender).DataContext;

            this.AncsManager.OnAction(not, false);
        }

        /// <summary>
        /// Invoked as an event handler when the status of the publisher changes.
        /// </summary>
        /// <param name="publisher">Instance of publisher that triggered the event.</param>
        /// <param name="eventArgs">Event data containing information about the publisher status change event.</param>
        private async void OnAdvertiserStatusChanged(
            BluetoothLEAdvertisementPublisher publisher,
            BluetoothLEAdvertisementPublisherStatusChangedEventArgs eventArgs)
        {
            // This event handler can be used to monitor the status of the publisher.
            // We can catch errors if the publisher is aborted by the system
            BluetoothLEAdvertisementPublisherStatus status = eventArgs.Status;
            BluetoothError error = eventArgs.Error;

            if (error == BluetoothError.Success)
            {
                setStatus(status.ToString());

                switch (status)
                {
                    case BluetoothLEAdvertisementPublisherStatus.Started:
                        setStatus("Connecting...");
                        var connectionResult = await this.AncsManager.Connect();
                        if (connectionResult == true)
                        {
                            setStatus("Waiting for device...");
                        }
                        else
                        {
                            setStatus("No suitable device");
                        }

                        break;
                }
            }
            else
            {
                setStatus(String.Format("Error: {0}", error.ToString()));
            }
        }

        private async void OnAncsManagerConnectionStatusChanged(BluetoothLEDevice device, object args)
        {
            switch (device.ConnectionStatus)
            {
                case BluetoothConnectionStatus.Connected:
                    setStatus("Connected");
                    break;
                case BluetoothConnectionStatus.Disconnected:
                    setStatus("Disconnected");
                    break;
                default:
                    setStatus("Unknown");
                    break;
            }
        }
    }
}
