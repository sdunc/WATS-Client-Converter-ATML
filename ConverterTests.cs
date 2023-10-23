using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Text;
using Virinco.WATS.Interface;
using System.IO;

namespace ATMLConverter
{
    [TestClass]
    public class ConverterTests : TDM
    {
        [TestMethod]
        public void SetupClient()
        {
            SetupAPI(null, "", "Test", true);
            RegisterClient("your wats", "username", "password");
            InitializeAPI(true);
        }

        [TestMethod]
        public void TestATMLConverter()
        {
            InitializeAPI(true);
            ATMLConverter converter = new ATMLConverter();
            using (FileStream file = new FileStream(@"Examples\ATML 2.02 [123456789][2 30 27 PM][5 15 2019].XML", FileMode.Open))
            {
                converter.ImportReport(this, file);
            }
        }
    }
}
