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
using Windows.Data.Xml.Dom;
using NotificationsExtensions;
using Microsoft.QueryStringDotNET;

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
            XmlDocument toastXml = null;

            ToastVisual toastVisual = new ToastVisual()
            {
                BindingGeneric = new ToastBindingGeneric()
                {
                    Children = {
                        new AdaptiveText()
                        {
                            Text = o.Title
                        },
                        new AdaptiveText
                        {
                            Text = o.Message
                        }
                    }
                },
            };

            // toast actions
            ToastActionsCustom toastActions = new ToastActionsCustom();

            switch (o.CategoryId)
            {
                case CategoryId.IncomingCall:
                    //toastVisual.BindingGeneric.AppLogoOverride = new ToastGenericAppLogo() { Source = "Assets/iOS7_App_Icon_Phone.png" };
                    toastActions.Buttons.Add(new ToastButton("Answer", new QueryString() {
                        {"action", "answer"},
                        {"uid", o.Uid.ToString() }
                    }.ToString())
                    {
                        ActivationType = ToastActivationType.Foreground
                    });

                    //toastVisual.BindingGeneric.AppLogoOverride = new ToastGenericAppLogo() { Source = "Assets/iOS7_App_Icon_Phone.png" };
                    toastActions.Buttons.Add(new ToastButton("Dismiss", new QueryString() {
                        {"action", "dismiss"},
                        {"uid", o.Uid.ToString() }
                    }.ToString())
                    {
                        ActivationType = ToastActivationType.Foreground
                    });

                    break;
                case CategoryId.MissedCall:
                    //toastVisual.BindingGeneric.AppLogoOverride = new ToastGenericAppLogo() { Source = "Assets/iOS7_App_Icon_Phone.png" };
                    toastActions.Buttons.Add(new ToastButtonDismiss());
                    break;
                case CategoryId.Email:
                    //toastVisual.BindingGeneric.AppLogoOverride = new ToastGenericAppLogo() { Source = "Assets/iOS7_App_Icon_Email.png" };
                    toastActions.Buttons.Add(new ToastButtonDismiss());
                    break;
                default:
                    toastActions.Buttons.Add(new ToastButtonDismiss());
                    break;
            }

            ToastContent toastContent = new ToastContent()
            {
                Visual = toastVisual,
                Scenario = ToastScenario.Default,
                Actions = toastActions,
            };

            toastXml = toastContent.GetXml();

            ToastNotification toastNotification = new ToastNotification(toastXml)
            {
                ExpirationTime = DateTime.Now.AddMinutes(5)
            };

            ToastNotificationManager.CreateToastNotifier().Show(toastNotification);

            // Old stuff
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                this.DataList.Add(o);
            });
        }


        private async void button_Click(object sender, RoutedEventArgs e)
        {
            setStatus("Service solicitation...");

            try
            {
                this.Advertiser.Stop();
                this.Advertiser.Start();
            }
            catch (Exception ex)
            {
                setStatus(ex.Message);
            }
        }



        private async void setStatus(string status)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                txtStatus.Text = status;
            });
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
                        try
                        {
                            var connectionTask = this.AncsManager.Connect();
                            var connectionResult = await connectionTask;

                            //var connectionResult = await this.AncsManager.Connect();
                            if (connectionResult == true)
                            {
                                setStatus("Waiting for device...");
                            }
                            else
                            {
                                setStatus("No suitable device");
                            }
                        }
                        catch (Exception ex)
                        {
                            setStatus(ex.Message);
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
