using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experior.Plugin;
using System.Globalization;
using Experior.Core.Communication;

namespace Experior.Controller.TollFashion
{
    public class ZplLabeler
    {
        private readonly ZplSocket cartonLabelerConnection;
        private readonly Queue<string> barcodes = new Queue<string>();
        private static int noBarcodesCount;

        public ZplLabeler(string connectionName)
        {
            cartonLabelerConnection = Connection.Items.Values.OfType<ZplSocket>().FirstOrDefault(c => c.Name == connectionName);
            if (cartonLabelerConnection != null)
            {
                cartonLabelerConnection.OnMessageReceived += CartonLabelerConnectionOnMessageReceived;
                //cartonLabelerConnection.ZplScriptReceived += CartonLabelerConnection_ZplScriptReceived;
            }
        }

        //private void CartonLabelerConnection_ZplScriptReceived(object sender, string zpl)
        //{
        //    var barcode = ExtractBarcode(zpl);
        //    var conn = sender as Connection;
        //    if (!string.IsNullOrWhiteSpace(barcode))
        //    {
        //        Core.Environment.Invoke(() =>
        //            {
        //                Log.Write(DateTime.Now.ToString(CultureInfo.InvariantCulture) + " MFH>PRT: " + conn?.Id + " " + zpl);
        //                Log.Write($"Barcode extracted from ZPL script: {barcode}");
        //                barcodes.Enqueue(barcode);
        //            }
        //        );
        //    }
        //}

        private void CartonLabelerConnectionOnMessageReceived(Connection sender, byte[] message)
        {
            var zpl = Encoding.ASCII.GetString(message);
            var barcode = ExtractBarcode(zpl);
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                Core.Environment.Invoke(() =>
                    {
                        Log.Write(DateTime.Now.ToString(CultureInfo.InvariantCulture) + " MFH>PRT: " + sender.Id + " " + zpl);
                        Log.Write($"Barcode extracted from ZPL script: {barcode}");
                        barcodes.Enqueue(barcode);
                    }
                );
            }
        }

        private string ExtractBarcode(string zpl)
        {
            try
            {
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
            var nextBarcode = barcodes.Any() ? barcodes.Dequeue() : $"?{(++noBarcodesCount).ToString("D6")}?";
            return nextBarcode;
        }

        public void Reset()
        {
            barcodes.Clear();
        }
    }
}