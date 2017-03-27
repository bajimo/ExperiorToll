using Experior.Core.Loads;
using Experior.Core.TransportSections;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Experior.Dematic.Base
{
    public static class FeedLoad
    {
        public static long nextSSCC = 55710319008100001;
        public static int barcodeLength = 10;

        //  public static List<ICaseControllerCommon> CaseControllers = new List<ICaseControllerCommon>(); 

        //Feed case load should actually be part of the basic code for BK10 (perhaps common)
        //Then we want to add default and selectable types of loads that can be configured within a project
        //These types will then be used to generate loads and add them to the model at the correct place
        //For now we just have a default type which cannot be changed on a project specific basis 
        public static Case_Load FeedCaseLoad(ITransportSection transportSection, float distance, BaseCaseData caseData)
        {
            Case_Load caseLoad;
            IEmulationController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IEmulationController) as IEmulationController;

            if (controller != null)
            {
                caseLoad = controller.GetCaseLoad(transportSection.Route, 0);
                if (caseLoad != null) //It is not necesary to implement this just return null case load from the controller and a standard load will be created
                {
                    return caseLoad;
                }
            }

            try
            {
                string SSCCBarcode = "";

                MeshInfo boxInfo = new MeshInfo();
                boxInfo.color = System.Drawing.Color.Peru;
                boxInfo.filename = Case_Load.GraphicsMesh;

                SSCCBarcode = GetSSCCBarcode();
                boxInfo.length = 0.6f;
                boxInfo.width = 0.4f;
                boxInfo.height = 0.28f;

                Case_Load boxLoad = new Case_Load(boxInfo);
                boxLoad.Case_Data = caseData;
                boxLoad.Case_Data.Weight = 2.3f;
                transportSection.Route.Add(boxLoad, distance);
                Experior.Core.Loads.Load.Items.Add(boxLoad);

                if (SSCCBarcode != "")
                {
                    boxLoad.SSCCBarcode = SSCCBarcode;
                    boxLoad.Identification = SSCCBarcode;
                }
                return boxLoad;

            }
            catch (Exception se)
            {
                Core.Environment.Log.Write(se);
                Core.Environment.Scene.Pause();
                return null;
            }
        }
        
        public static Case_Load FeedCaseLoad(ITransportSection transportSection, float distance, float length, float width, float height, float weight, Color color, int barcodeLength, BaseCaseData caseData)
        {
            try
            {
                string SSCCBarcode = "";

                MeshInfo boxInfo = new MeshInfo();
                boxInfo.color = color;
                boxInfo.filename = Case_Load.GraphicsMesh;

                SSCCBarcode = GetSSCCBarcode();// GetSSCCBarcode(barcodeLength);
                boxInfo.length = length;
                boxInfo.width = width;
                boxInfo.height = height;

                Case_Load boxLoad = new Case_Load(boxInfo);
                boxLoad.Case_Data = caseData;
                boxLoad.Case_Data.Weight = weight;
                transportSection.Route.Add(boxLoad, distance);
                Experior.Core.Loads.Load.Items.Add(boxLoad);

                if (SSCCBarcode != "")
                    boxLoad.SSCCBarcode = SSCCBarcode;
                return boxLoad;

            }
            catch (Exception se)
            {
                Core.Environment.Log.Write(se);
                Core.Environment.Scene.Pause();
                return null;
            }
        }
               
        /// <summary>
        /// Method to return a Tray load
        /// </summary>
        /// <param name="transportSection">The transport section to add the load too</param>
        /// <param name="distance">The position distance along the transport section</param>
        /// <param name="trayData">Base tray data</param>
        /// <param name="trayStatus">Tray status (Empty, Loaded or Stacked)</param>
        /// <returns>Load of type Tray</returns>
        public static Tray FeedTrayLoad(ITransportSection transportSection, float distance, BaseCaseData trayData, TrayStatus trayStatus)
        {
            Tray trayLoad;
            IEmulationController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IEmulationController) as IEmulationController;

            if (controller != null)
            {
                trayLoad = controller.GetTray(transportSection.Route, 0, trayStatus);
                if (trayLoad != null) //It is not necesary to implement this just return null case load from the controller and a standard load will be created
                {
                    return trayLoad;
                }
            }
            try
            {
                TrayInfo trayInfo = new TrayInfo();
                trayInfo.color = Color.Peru;
                trayInfo.filename = Tray.Mesh;
                trayInfo.Status = trayStatus;
                trayInfo.TrayStacks = 6;

                //LoadHeight includes the height of the tray (280mm)
                trayInfo.LoadHeight = 0.410f;
                trayInfo.LoadWidth = 0.4f;
                trayInfo.LoadLength = 0.6f;

                //Set the dimensions of a tray (This is the standard size)
                trayInfo.length = 0.65f;
                trayInfo.width = 0.45f;
                trayInfo.height = 0.058f; // Actual size is 0.063f but reduced so visible space can be added in stack (0.005f space)

                trayLoad = new Tray(trayInfo);

                trayLoad.Case_Data = trayData;
                transportSection.Route.Add(trayLoad, distance);
                Load.Items.Add(trayLoad);

                string SSCCBarcode = GetSSCCBarcode();
                if (SSCCBarcode != "")
                {
                    trayLoad.SSCCBarcode = SSCCBarcode;
                    trayLoad.Identification = SSCCBarcode;
                }
                return trayLoad;

            }
            catch (Exception se)
            {
                Core.Environment.Log.Write(se);
                Core.Environment.Scene.Pause();
                return null;
            }
        }

        public static EuroPallet FeedEuroPallet(ITransportSection transportSection, float distance, BasePalletData palletData, PalletStatus palletStatus)
        {
            EuroPallet PalletLoad;
            IEmulationController controller = Core.Assemblies.Assembly.Items.Values.ToList().FirstOrDefault(x => x is IEmulationController) as IEmulationController;

            if (controller != null)
            {
                PalletLoad = controller.GetEuroPallet(transportSection.Route, 0, palletStatus);
                if (PalletLoad != null) //It is not necesary to implement this just return null Pallet load from the controller and a standard load will be created
                {
                    return PalletLoad;
                }
            }
            try
            {
                EuroPalletInfo palletInfo = new EuroPalletInfo();
                palletInfo.color = Color.Peru;
                palletInfo.Status = palletStatus;
                palletInfo.filename = EuroPallet.Mesh;

                //LoadHeight includes the height of the pallet (145mm)
                palletInfo.LoadHeight = 2.2f;
                palletInfo.LoadWidth =  0.9f;
                palletInfo.LoadLength = 1.3f; 

                //Set the dimensions of a EuroPallet (This is the standard size)
                palletInfo.length = 1.2f;
                palletInfo.width = 0.8f;
                palletInfo.height = 0.14f; // Actual size is 0.144f but reduced so visible space can be added in stack (0.005f space)

                EuroPallet palletLoad = new EuroPallet(palletInfo);
                
                //palletLoad.Part = (RigidLoadPart)new PalletPart(0.008f, palletInfo.color, palletInfo.length, palletInfo.height, palletInfo.width, true, palletInfo.density, Pallet_Load.Size(PalletType.EuroPallet), palletInfo.rigid);
                palletLoad.Pallet_Data = palletData;
                palletLoad.Pallet_Data.Weight = 60.0f;
                transportSection.Route.Add(palletLoad, distance);
                Load.Items.Add(palletLoad);

                string SSCCBarcode = GetSSCCBarcode();
                if (SSCCBarcode != "")
                {
                    palletLoad.SSCCBarcode = SSCCBarcode;
                    palletLoad.Identification = SSCCBarcode;
                }
                return palletLoad;

            }
            catch (Exception se)
            {
                Core.Environment.Log.Write(se);
                Core.Environment.Scene.Pause();
                return null;
            }
        }


        public static string GetSSCCBarcode()
        {
            //Look for a text file called Barcode.txt in the Experior file
            string barcodeFile = Environment.CurrentDirectory + "\\Barcode.txt";
            string result = null;
            long currentBarcode;

            if (File.Exists(barcodeFile) && long.TryParse(File.ReadAllText(barcodeFile), out currentBarcode))
            {
                result = currentBarcode.ToString();
                currentBarcode++;
                if (currentBarcode > 55710319819999999)
                {
                    currentBarcode = 55710319810000001;
                }
                File.WriteAllText(barcodeFile, currentBarcode.ToString());
            }
            else
            {
                result = NextSSCC.ToString();
                NextSSCC++;
            }
            result = GetSSCCWithCheckSum(result);
            result = result.Substring(result.Length - BarcodeLength, BarcodeLength);

            return result;
        }

        /// <summary>
        /// Get SSCC barcode but define the barcode length
        /// </summary>
        /// <param name="barcodelength">Length of Barcode</param>
        /// <returns></returns>
        //public static string GetSSCCBarcode(int barcodelength)
        //{
        //    string result = NextSSCC.ToString();

        //    NextSSCC++;

        //    result = GetSSCCWithCheckSum(result);

        //    result = result.Substring(result.Length - BarcodeLength, barcodeLength);

        //    return result;
        //}

        /// <summary>
        /// Takes 17 digit sscc (without checksum) and returns 18 digit sscc (with checksum).
        /// See http://www.gs1.org/barcodes/support/check_digit_calculator/
        /// </summary>
        /// <param name="ssccstring">17 digit sscc (without checksum)</param>
        /// <returns>18 digit sscc (with checksum)</returns>
        public static string GetSSCCWithCheckSum(string ssccstring)
        {
            long sscc = long.Parse(ssccstring);

            if (ssccstring.Length != 17)
            {
                throw new Exception("sscc must be 17 digits to calculate check sum");
            }

            long[] digits = NumbersIn(sscc);

            //Step 1: Multiply value of each position by 1 or 3

            for (int i = 0; i < 17; i++)
            {
                if (i % 2 == 0)
                    digits[i] = digits[i] * 3;
            }

            //Step 2: Add results together to create sum
            long sum = 0;
            foreach (long digit in digits)
                sum += digit;

            //Step 3: Subtract the sum from nearest equal or higher multiple of ten = Check Digit
            long checksum = sum % 10;
            checksum = 10 - checksum;
            checksum = checksum % 10;

            ssccstring += checksum.ToString();

            if (ssccstring.Length != 18)
            {
                throw new Exception("error calculating sscc check sum (not 18 digits!)");
            }

            return ssccstring;
        }

        [Category("Configuration")]
        [DisplayName("Next SSCC")]
        [Description("Next tote created will get this SSCC. First 17 characters of SSCC. Checksum will be added ")]
        //[PropertyOrder(9)]
        public static long NextSSCC
        {
            get
            {
                return nextSSCC;
            }
            set
            {
                nextSSCC = value;
            }
        }

        [Category("Configuration")]
        [DisplayName("Barcode length")]
        [Description("Length of SSCC barcode.")]
        //[PropertyOrder(10)]
        public static int BarcodeLength
        {
            get
            {
                return barcodeLength;
            }
            set
            {
                if (value > 0 && value <= 18)
                    barcodeLength = value;
            }
        }

        private static long[] NumbersIn(long value)
        {
            var numbers = new Stack<long>();

            for (; value > 0; value /= 10)
                numbers.Push(value % 10);

            return numbers.ToArray();
        }

    }

}
