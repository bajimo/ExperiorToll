using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Plugin;

namespace Experior.Controller.TollFashion
{
    public class ZplLabeler
    {
        private readonly ZplSocket cartonLabelerConnection;
        private readonly Queue<string> barcodes = new Queue<string>();
        private static int noBarcodesCount;

        public ZplLabeler(string connectionName)
        {
            cartonLabelerConnection = Core.Communication.Connection.Items.Values.OfType<ZplSocket>().FirstOrDefault(c => c.Name == connectionName);
            if (cartonLabelerConnection != null)
            {
                cartonLabelerConnection.OnMessageReceived += CartonLabelerConnectionOnMessageReceived;
            }
        }

        private void CartonLabelerConnectionOnMessageReceived(Core.Communication.Connection sender, byte[] message)
        {
            var barcode = ExtractBarcode(message);
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                Core.Environment.Invoke(() =>
                    {
                        Log.Write($"{sender.Name} barcode extracted from ZPL script: {barcode}");
                        barcodes.Enqueue(barcode);
                    }
                );
            }
        }

        private string ExtractBarcode(byte[] zplMessage)
        {
            try
            {
                var zpl = Encoding.ASCII.GetString(zplMessage);
                var startIndex = zpl.IndexOf("^FD>;", StringComparison.Ordinal) + 5;
                var endIndex = zpl.IndexOf("^FS", StringComparison.Ordinal);
                var barcode = zpl.Substring(startIndex, endIndex - startIndex).Trim();
                return barcode;
            }
            catch (Exception e)
            {
                Log.Write(e.ToString());
                return string.Empty;
            }
        }

        public string GetNextValidBarcode()
        {
            var nextBarcode = barcodes.Any() ? barcodes.Dequeue() : $"noBarcodes {++noBarcodesCount}";
            return nextBarcode;
        }

        public void Reset()
        {
            barcodes.Clear();
        }
    }
}