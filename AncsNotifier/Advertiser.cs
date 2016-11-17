using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;

namespace AncsNotifier
{
    public class Advertiser
    {
        public event Windows.Foundation.TypedEventHandler<BluetoothLEAdvertisementPublisher, BluetoothLEAdvertisementPublisherStatusChangedEventArgs> StatusChanged;

        private static readonly byte[] SolicitationData = {
             // flags
                0x02,
                0x01, //GAP_ADTYPE_FLAGS
                0x02, // GAP_ADTYPE_FLAGS_GENERAL
                //Solicitation
                0x11,
                0x15, //GAP_ADTYPE_SERVICES_LIST_128BIT
                // ANCS service UUID (little endian)
                0xD0, 0x00, 0x2D, 0x12, 0x1E, 0x4B, 0x0F, 0xA4, 0x99, 0x4E, 0xCE, 0xB5, 0x31, 0xF4, 0x05, 0x79
        };

        private static readonly UInt16 ManufacturerId = 0x010E;

        private BluetoothLEAdvertisementPublisher _publisher;

        public BluetoothLEAdvertisementPublisherStatus Status
        {
            get {
                if (_publisher == null) {
                    return BluetoothLEAdvertisementPublisherStatus.Created;
                }
                return _publisher.Status;
            }
        }

        public void Start()
        {
            // Create and initialize a new publisher instance.
            this._publisher = new BluetoothLEAdvertisementPublisher();

            // Attach a event handler to monitor the status of the publisher, which
            // can tell us whether the advertising has been serviced or is waiting to be serviced
            // due to lack of resources. It will also inform us of unexpected errors such as the Bluetooth
            // radio being turned off by the user.
            this._publisher.StatusChanged += OnPublisherStatusChanged;

            // We need to add some payload to the advertisement. A publisher without any payload
            // or with invalid ones cannot be started. We only need to configure the payload once
            // for any publisher.

            // Add a manufacturer-specific section:
            // First, let create a manufacturer data section
            var manufacturerData = new BluetoothLEManufacturerData();

            // Then, set the company ID for the manufacturer data. Here we picked an unused value: 0xFFFE
            manufacturerData.CompanyId = ManufacturerId;

            // Finally set the data payload within the manufacturer-specific section
            // Here, use a 16-bit UUID: 0x1234 -> {0x34, 0x12} (little-endian)
            var writer = new DataWriter();
            UInt16 uuidData = 0x1234;
            writer.WriteUInt16(uuidData);

            // Make sure that the buffer length can fit within an advertisement payload. Otherwise you will get an exception.
            manufacturerData.Data = writer.DetachBuffer();

            // Add the manufacturer data to the advertisement publisher:
            this._publisher.Advertisement.ManufacturerData.Add(manufacturerData);

            // From old code (Which is not commented)
            var data = new BluetoothLEAdvertisementDataSection { Data = SolicitationData.AsBuffer() };

            this._publisher.Advertisement.DataSections.Add(data);

            try
            {
                // Calling publisher start will start the advertising if resources are available to do so
                this._publisher.Start();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Stop()
        {
            if (this._publisher != null)
            {
                this._publisher.Stop();
                this._publisher = null;
            }
        }

        /// <summary>
        /// Invoked as an event handler when the status of the publisher changes.
        /// </summary>
        /// <param name="publisher">Instance of publisher that triggered the event.</param>
        /// <param name="eventArgs">Event data containing information about the publisher status change event.</param>
        private async void OnPublisherStatusChanged(
            BluetoothLEAdvertisementPublisher publisher,
            BluetoothLEAdvertisementPublisherStatusChangedEventArgs eventArgs)
        {
            // This event handler can be used to monitor the status of the publisher.
            // We can catch errors if the publisher is aborted by the system
            BluetoothLEAdvertisementPublisherStatus status = eventArgs.Status;
            BluetoothError error = eventArgs.Error;

            StatusChanged?.Invoke(publisher, eventArgs);
        }
    }
}
