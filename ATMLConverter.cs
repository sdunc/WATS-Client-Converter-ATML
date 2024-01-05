using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ATMLConverter.ATML202.schema;
using ATMLConverter.ATML50.schema;
using ATMLConverter.ATML601.schema;
using Virinco.WATS.Interface;

namespace ATMLConverter
{
    public class ATMLConverter : IReportConverter_v2
    {
        public void CleanUp()
        {
        }

        Dictionary<string, string> arguments = new Dictionary<string, string>
        {
            { "operationTypeCode","10"},
            { "partNumber","NA"},
            { "partRevision","10"},
            { "operator","oper"},
            { "sequenceVersion","0.0.0"}
        };

        public Dictionary<string, string> ConverterParameters => arguments;

        public ATMLConverter() { }

        public ATMLConverter(Dictionary<string, string> args)
        {
            arguments = args;
        }

        private string GetArgument(string argumentName)
        {
            if (arguments.ContainsKey(argumentName))
                return arguments[argumentName];
            else
                return ConverterParameters[argumentName];
        }

        /*
         202:
            <tr:TestResults xmlns:tr="http://www.ieee.org/ATML/2007/TestResults" xmlns:c="http://www.ieee.org/ATML/2006/Common" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:ts="www.ni.com/TestStand/ATMLTestResults/1.0" uuid="aaba70f8-7190-11e9-9276-9822efe210a8">
         50:
            <trc:TestResultsCollection xmlns:trc="urn:IEEE-1636.1:2011:01:TestResultsCollection" xmlns:tr="urn:IEEE-1636.1:2011:01:TestResults" xmlns:c="urn:IEEE-1671:2010:Common" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:ts="www.ni.com/TestStand/ATMLTestResults/2.0"> 
            <trc:TestResults uuid="3a4e9dfb-7191-11e9-9276-9822efe210a8" xmlns:trc="urn:IEEE-1636.1:2011:01:TestResultsCollection" xmlns:tr="urn:IEEE-1636.1:2011:01:TestResults" xmlns:c="urn:IEEE-1671:2010:Common" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:ts="www.ni.com/TestStand/ATMLTestResults/2.0">
         601:
            <trc:TestResultsCollection xmlns:trc="urn:IEEE-1636.1:2013:TestResultsCollection" xmlns:tr="urn:IEEE-1636.1:2013:TestResults" xmlns:c="urn:IEEE-1671:2010:Common" xmlns:sc="urn:IEEE-1636.99:2013:SimicaCommon" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:ts="www.ni.com/TestStand/ATMLTestResults/3.0">
            <trc:TestResults uuid="5b360926-7191-11e9-9276-9822efe210a8" xmlns:trc="urn:IEEE-1636.1:2013:TestResultsCollection" xmlns:tr="urn:IEEE-1636.1:2013:TestResults" xmlns:c="urn:IEEE-1671:2010:Common" xmlns:sc="urn:IEEE-1636.99:2013:SimicaCommon" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:ts="www.ni.com/TestStand/ATMLTestResults/3.0">
        */

        XNamespace trc, tr, ts, xsi, c;

        void GetNameSpaces(XElement rootElement, out XNamespace trc, out XNamespace tr, out XNamespace ts, out XNamespace xsi, out XNamespace c)
        {
            if (rootElement.Name.NamespaceName == "http://www.ieee.org/ATML/2007/TestResults")
            {
                trc = null;
                tr = rootElement.Name.NamespaceName;
                ts = "www.ni.com/TestStand/ATMLTestResults/1.0";
                xsi = "http://www.w3.org/2001/XMLSchema-instance";
                c = "http://www.ieee.org/ATML/2006/Common";
            }
            else if (rootElement.Name.NamespaceName == "urn:IEEE-1636.1:2011:01:TestResultsCollection")
            {
                trc = rootElement.Name.NamespaceName;
                tr = "urn:IEEE-1636.1:2011:01:TestResults";
                ts = "www.ni.com/TestStand/ATMLTestResults/2.0";
                xsi = "http://www.w3.org/2001/XMLSchema-instance";
                c = "urn:IEEE-1671:2010:Common";
            }
            else if (rootElement.Name.NamespaceName == "urn:IEEE-1636.1:2013:TestResultsCollection")
            {
                trc = rootElement.Name.NamespaceName;
                tr = "urn:IEEE-1636.1:2013:TestResults";
                ts = "www.ni.com/TestStand/ATMLTestResults/3.0";
                xsi = "http://www.w3.org/2001/XMLSchema-instance";
                c = "urn:IEEE-1671:2010:Common";
            }
            else
                throw new NotSupportedException("Unsupported ATML Format. Supported formats: 2.02,5.0,6.01");
        }

        public Report ImportReport(TDM api, Stream file)
        {
            api.TestMode = TestModeType.Import;
            api.ValidationMode = ValidationModeType.AutoTruncate;
            System.Xml.XmlReader reader = System.Xml.XmlReader.Create(file);
            XDocument atml = XDocument.Load(reader);
            GetNameSpaces(atml.Root, out trc, out tr, out ts, out xsi, out c);
            if (trc == null) //no collection container, root is Testresults
            {
                UUTReport uut = CreateReportHeader(api, atml.Root);
                SequenceCall currentSequence = uut.GetRootSequenceCall();
                ProcessElements(uut, atml.Root.Element(tr + "ResultSet").Element(tr + "TestGroup"), currentSequence);
                ProcessResultSetProperties(uut, atml.Element(tr + "TestResults"));
                api.Submit(uut);
            }
            else
            {
                foreach (XElement testResults in atml.Root.Elements(trc + "TestResults"))
                {
                    UUTReport uut = CreateReportHeader(api, testResults);
                    ProcessResultSet(api, uut, testResults);
                    ProcessResultSetProperties(uut, testResults);
                    api.Submit(uut);
                }
            }
            return null;
        }


        private void ProcessResultSet(TDM api, UUTReport uut, XElement testResults)
        {
            SequenceCall currentSequence = uut.GetRootSequenceCall();
            ProcessElements(uut, testResults.Element(tr + "ResultSet"), currentSequence);
        }

        private void ProcessResultSetProperties(UUTReport uut, XElement testResults)
        {
            XElement TSResultSetProperties = testResults.Element(tr + "Extension").Element(ts + "TSResultSetProperties");
            if (TSResultSetProperties != null)
            {
                XElement TestSocketIndex = TSResultSetProperties.Element(ts + "TestSocketIndex");
                if (TestSocketIndex != null
                    && short.TryParse(TSResultSetProperties.Element(ts + "TestSocketIndex").Value, out short tsi))
                    uut.TestSocketIndex = tsi;


                XElement batchSerialNumber = TSResultSetProperties.Element(ts + "BatchSerialNumber");
                if (batchSerialNumber != null)
                    uut.BatchSerialNumber = batchSerialNumber.Value;
            }
        }

        void ProcessElements(UUTReport uut, XElement element, SequenceCall currentSequence)
        {
            string callerName = element.Attribute("callerName")?.Value;
            string groupName = element.Attribute("name").Value;
            //if (!groupName.Contains("#"))
            //    groupName = element.Element(tr + "TestGroup").Attribute("name").Value; //Diff between ATML 1.0 and 2.0
            string[] splitName = groupName.Split(new char[] { '#' });
            currentSequence.SequenceName = splitName[0];

            if (!string.IsNullOrEmpty(callerName))
                currentSequence.Name = callerName;
            else if (splitName.Length > 1)
                currentSequence.Name = splitName[1];
            else
                currentSequence.Name = "";
            
            SetStepProperties(element.Element(tr + "Extension").Element(ts + "TSStepProperties"), currentSequence);
            currentSequence.Status = GetOutcome(element.Element(tr + "Outcome"));
            foreach (XElement subElement in element.Elements())
            {
                XElement extension = subElement.Element(tr + "Extension");
                if (extension != null)
                {
                    XElement properties = subElement.Element(tr + "Extension").Element(ts + "TSStepProperties");

                    if (properties == null)
                        throw new InvalidOperationException($"{subElement.Name.LocalName} ID = \"{subElement.Attribute("ID").Value}\" in {element.Name.LocalName} ID = \"{element.Attribute("ID").Value}\" is not a step.");

                    string stepType = properties.Element(ts + "StepType").Value;

                    //if (properties != null) //If we want to avoid throwing errors and want the conversion to continue.
                    //    stepType = properties.Element(ts + "StepType").Value;
                    //else
                    //    stepType = "";

                    Step step = null;
                    if (subElement.Name.LocalName == "TestGroup")
                    {
                        SequenceCall newSequence = currentSequence.AddSequenceCall("SequenceCall");
                        step = newSequence;
                        ProcessElements(uut, subElement, newSequence);
                    }
                    else if (subElement.Name.LocalName == "SessionAction")
                    {
                        GenericStepTypes genericStepType = GenericStepTypes.Action;
                        stepType = stepType.Replace("Statement", "Action");
                        try //Try to use Icontypes to interpret
                        {
                            iconNames icon = (iconNames)Enum.Parse(typeof(iconNames), stepType);
                            genericStepType = (GenericStepTypes)icon;
                        }
                        catch (Exception) { };
                        step = currentSequence.AddGenericStep(genericStepType, subElement.Attribute("name").Value);
                        step.Status = GetOutcome(subElement.Element(tr + "ActionOutcome"));
                    }
                    else if (subElement.Name.LocalName == "Test")
                    {
                        var stepOutcome = GetOutcome(subElement.Element(tr + "Outcome"));
                        switch (stepType)
                        {
                            case "PassFailTest":
                                step = currentSequence.AddPassFailStep(subElement.Attribute("name").Value);
                                ((PassFailStep)step).AddTest(stepOutcome != StepStatusType.Failed);
                                break;
                            case "StringValueTest":
                                step = currentSequence.AddStringValueStep(subElement.Attribute("name").Value);

                                // Filter out additional results according to TestStand ATML stylesheet
                                XElement strTestResult = subElement.Elements(tr + "TestResult").FirstOrDefault(e =>
                                    e.Element(tr + "TestLimits") != null
                                    || e.Element(tr + "Extension")?.Element(ts + "TSLimitProperties")?.Element("IsComparisonTypeLog") != null
                                    || e.Attribute("Name")?.Value == "ReportText"
                                    || e.Attribute("name")?.Value == "ReportText"
                                );

                                if (strTestResult != null)
                                {
                                    string strValue = strTestResult.Element(tr + "TestData")?.Element(c + "Datum")?.Element(c + "Value")?.Value ?? "";
                                    string strLimit; CompOperatorType strCompOp;
                                    
                                    //Check if step is set to or defaulted to Done. Set test as passed if this is true or Step will throw an exception.
                                    StepStatusType testOutcome = stepOutcome == StepStatusType.Done ? StepStatusType.Passed : stepOutcome;

                                    GetStrLimits(strTestResult.Element(tr + "TestLimits"), out strLimit, out strCompOp);
                                    ((StringValueStep)step).AddTest(strCompOp, strValue, strLimit, testOutcome);
                                }
                                break;
                            case "NumericLimitTest":
                            case "NI_MultipleNumericLimitTest":
                                NumericLimitStep numericLimitStep = currentSequence.AddNumericLimitStep(subElement.Attribute("name").Value);

                                // Filter out additional results according to TestStand ATML stylesheet
                                XElement[] testResults = subElement.Elements(tr + "TestResult").Where(e =>
                                    e.Element(tr + "TestLimits") != null
                                    || e.Element(tr + "Extension")?.Element(ts + "TSLimitProperties")?.Element(ts + "IsComparisonTypeLog") != null
                                    || e.Attribute("Name")?.Value == "ReportText"
                                    || e.Attribute("name")?.Value == "ReportText"
                                ).ToArray();

                                if (testResults.Length > 0)
                                {
                                    foreach (XElement testResult in testResults)
                                    {
                                        string strValue = testResult.Element(tr + "TestData")?.Element(c + "Datum")?.Attribute("value")?.Value ?? "0";
                                        double value = Str2Double(strValue);
                                        string unit = testResult.Element(tr + "TestData")?.Element(c + "Datum")?.Attribute("nonStandardUnit")?.Value ?? "";
                                        double low, high; CompOperatorType compOp;
                                        StepStatusType outcome = testResult.Element(tr + "Outcome") != null ? GetOutcome(testResult.Element(tr + "Outcome")) : stepOutcome;

                                        //Check if step is set to or defaulted to Done. Set test as passed if this is true or Step will throw an exception.
                                        StepStatusType testOutcome = outcome == StepStatusType.Done ? StepStatusType.Passed : outcome;
                                        XElement limits = testResult.Element(tr + "TestLimits");
                                        if (limits == null)
                                        {
                                            if (testResults.Length == 1)
                                                numericLimitStep.AddTest(value, unit, testOutcome);
                                            else
                                                numericLimitStep.AddMultipleTest(value, unit, testResult.Attribute("name")?.Value, testOutcome);
                                        }
                                        else
                                        {
                                            GetNumLimits(limits, out low, out high, out compOp);
                                            if (testResults.Length == 1)
                                            {
                                                if (compOp.ToString().Length > 2)
                                                    numericLimitStep.AddTest(value, compOp, low, high, unit, testOutcome);
                                                else
                                                    numericLimitStep.AddTest(value, compOp, low, unit, testOutcome);
                                            }
                                            else
                                            {
                                                string measureName = (testResult.Attribute("name")?.Value) ?? (testResult.Attribute("ID")?.Value);
                                                if (compOp.ToString().Length > 2)
                                                    numericLimitStep.AddMultipleTest(value, compOp, low, high, unit, measureName, testOutcome);
                                                else
                                                    numericLimitStep.AddMultipleTest(value, compOp, low, unit, measureName, testOutcome);
                                            }
                                        }
                                    }
                                }
                                step = numericLimitStep;
                                break;
                        }
                        if (step != null) step.Status = stepOutcome;
                    }
                    if (step != null)
                    {
                        SetStepProperties(properties, step);
                        step.ReportText = subElement.Element(tr + "Data")?.Element(c + "Collection")?.
                            Elements(c + "Item")?.Where(i => i.Attribute("name").Value == "ReportText").FirstOrDefault()?.
                            Element(c + "Datum")?.Element(c + "Value")?.Value ?? "";
                        //if (stepType=="AdditionalResults")
                        //{
                        //    XElement additionalResults = new XElement("Data");
                        //    foreach (XElement item in subElement.Element(tr+"Data").Element(c+"Collection").Elements(c+"Item"))
                        //    {
                        //        additionalResults.Add(new XElement("Name", item.Attribute("name").Value));
                        //        additionalResults.Add(new XElement("Value", item.Element(c + "Datum").Element(c+"Value").Value));
                        //    }
                        //    step.AddAdditionalResult("Item", additionalResults);
                        //}
                    }

                   
                    //TODO: ErrorCode, ErrorMessage....
                }
            }
        }

        double Str2Double(string s)
        {
            double result = double.NaN;
            if (s.Contains("0x"))
            {
                int iRes;
                if (int.TryParse(s.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out iRes))
                    result = iRes;
                else
                    throw new ApplicationException("Invalid hex number: " + s);
            }
            else if (!double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                switch (s.Trim().ToLower())
                {
                    case "infinity":
                    case "inf":
                    case "+infinity":
                    case "+inf":
                    case "positiveinfinity":
                        result = double.PositiveInfinity; break;
                    case "-infinity":
                    case "-inf":
                    case "negativeinfinity":
                        result = double.NegativeInfinity; break;
                    case "nan":
                    case "ind":
                        result = double.NaN; break;
                    default:
                        throw new ApplicationException("Invalid double number: " + s);
                }
            }
            return result;
        }

        StepStatusType GetOutcome(XElement outcome)
        {
            string value = outcome.Attribute("value").Value.Replace("NotStarted", "Skipped");
            if (value == "UserDefined" || value == "Aborted")
                value = outcome.Attribute("qualifier").Value;
            if (Enum.TryParse(value, out StepStatusType sst))
                return sst;
            else
                return StepStatusType.Done;
        }

        UUTStatusType GetUUTOutCome(XElement outcome)
        {
            StepStatusType stepStatusType = GetOutcome(outcome);
            return (UUTStatusType)Enum.Parse(typeof(UUTStatusType), stepStatusType.ToString());
        }

        private void GetNumLimits(XElement testLimits, out double low, out double high, out CompOperatorType compOp)
        {
            low = 0; high = 0; compOp = CompOperatorType.GELE;
            if (testLimits.Element(tr + "Limits").Element(c + "LimitPair") != null)
            {
                XElement[] limits = testLimits.Element(tr + "Limits").Element(c + "LimitPair").Elements(c + "Limit").ToArray();
                {
                    compOp = (CompOperatorType)Enum.Parse(typeof(CompOperatorType),
                        limits[0].Attribute("comparator").Value +
                        limits[1].Attribute("comparator").Value);
                    low = Str2Double(limits[0].Element(c + "Datum").Attribute("value").Value);
                    high = Str2Double(limits[1].Element(c + "Datum").Attribute("value").Value);
                }

            }
            else if (testLimits.Element(tr + "Limits").Element(c + "SingleLimit") != null)
            {
                compOp = (CompOperatorType)Enum.Parse(typeof(CompOperatorType), testLimits.Element(tr + "Limits").Element(c + "SingleLimit").Attribute("comparator").Value);
                low = Str2Double(testLimits.Element(tr + "Limits").Element(c + "SingleLimit").Element(c + "Datum").Attribute("value").Value);
            }
            else if (testLimits.Element(tr + "Limits").Element(c + "Expected") != null)
            {
                var limitProperties = testLimits.Element(tr + "Limits").Element(c + "Extension")?.Element(ts + "TSLimitProperties");
                if (limitProperties?.Element(ts + "ThresholdType") != null)
                {
                    double nominal_value = Str2Double(limitProperties.Element(ts + "RawLimits").Element(ts + "Nominal").Attribute("value").Value);
                    double threshold_low = Str2Double(limitProperties.Element(ts + "RawLimits").Element(ts + "Low").Attribute("value").Value);
                    double threshold_high = Str2Double(limitProperties.Element(ts + "RawLimits").Element(ts + "High").Attribute("value").Value);
                    switch (limitProperties.Element(ts + "ThresholdType").Value.ToLower())
                    {
                        case "percentage":
                            if (!double.IsNaN(threshold_low)) low = nominal_value - (Math.Abs(nominal_value) * threshold_low / 100);
                            if (!double.IsNaN(threshold_high)) high = nominal_value + (Math.Abs(nominal_value) * threshold_high / 100);
                            break;
                        case "ppm":
                            if (!double.IsNaN(threshold_low)) low = nominal_value - (Math.Abs(nominal_value) * threshold_low / 1000000);
                            if (!double.IsNaN(threshold_high)) high = nominal_value + (Math.Abs(nominal_value) * threshold_high / 1000000);
                            break;
                        case "delta":
                            if (!double.IsNaN(threshold_low)) low = nominal_value - threshold_low;
                            if (!double.IsNaN(threshold_high)) high = nominal_value + threshold_high;
                            break;
                    }

                    compOp = CompOperatorType.GELE;
                }
                else
                {
                    compOp = (CompOperatorType)Enum.Parse(typeof(CompOperatorType), testLimits.Element(tr + "Limits").Element(c + "Expected").Attribute("comparator").Value);
                    low = Str2Double(testLimits.Element(tr + "Limits").Element(c + "Expected").Element(c + "Datum").Attribute("value").Value);
                }
            }
        }

        private void GetStrLimits(XElement testLimits, out string limit, out CompOperatorType compOp)
        {
            compOp = CompOperatorType.LOG;
            limit = "";
            if (testLimits?.Element(tr + "Limits").Element(c + "Expected") != null)
            {
                string strCompOp = testLimits.Element(tr + "Limits").Element(c + "Expected").Attribute("comparator").Value;
                if (strCompOp == "CIEQ")
                    compOp = CompOperatorType.IGNORECASE;
                else
                    compOp = CompOperatorType.CASESENSIT;
                limit = testLimits.Element(tr + "Limits").Element(c + "Expected").Element(c + "Datum").Element(c + "Value").Value;
            }
        }


        private void SetStepProperties(XElement prop, Step step)
        {
            step.StepGroup = (StepGroupEnum)Enum.Parse(typeof(StepGroupEnum), prop.Element(ts + "StepGroup").Value);
            if (prop.Element(ts + "TotalTime") != null)
                step.StepTime = double.Parse(prop.Element(ts + "TotalTime").Attribute("value").Value, CultureInfo.InvariantCulture);
            if (prop.Element(ts + "ModuleTime") != null) step.ModuleTime = double.Parse(prop.Element(ts + "ModuleTime").Attribute("value").Value, CultureInfo.InvariantCulture);
        }

        private UUTReport CreateReportHeader(TDM api, XElement testResults)
        {
            XElement uutDefinition = testResults.Element(tr + "UUT")?.Element(c + "Definition");
            if (testResults.Element(tr + "UUT") == null)
                throw new ApplicationException("UUT Element not found in ATML report");
            UUTReport uutReport = api.CreateUUTReport(
                testResults.Element(tr + "Personnel")?.Element(tr + "SystemOperator")?.Attribute("ID")?.Value ?? GetArgument("operator"),
                uutDefinition?.Element(c + "Identification")?.Element(c + "IdentificationNumbers")?.Element(c + "IdentificationNumber")?.Attribute("number")?.Value ?? GetArgument("partNumber"),
                GetArgument("partRevision"),
                testResults.Element(tr + "UUT")?.Element(c + "SerialNumber")?.Value ?? "NA",
                GetArgument("operationTypeCode"),
                testResults.Element(tr + "ResultSet")?.Attribute("name").Value,
                GetArgument("sequenceVersion"));
            uutReport.StartDateTime= DateTime.ParseExact(testResults.Element(tr + "ResultSet")?.Attribute("startDateTime").Value, "yyyy-MM-ddTHH:mm:ss.FFFFF", CultureInfo.InvariantCulture);
            DateTime endDateTime = DateTime.ParseExact(testResults.Element(tr + "ResultSet")?.Attribute("endDateTime").Value, "yyyy-MM-ddTHH:mm:ss.FFFFF", CultureInfo.InvariantCulture);
            uutReport.ExecutionTime = (endDateTime - uutReport.StartDateTime).TotalSeconds;
            uutReport.StationName = testResults.Element(tr + "TestStation").Element(c + "SerialNumber").Value;
            uutReport.Status = GetUUTOutCome(testResults.Element(tr + "ResultSet").Element(tr + "Outcome"));

            //Additional Data handling
            XElement additionalData = uutDefinition?.Element(c + "Extension")?.Element(ts + "TSCollection")?
                .Elements()?.Where(e => e.Attribute("name")?.Value == "AdditionalData")?.FirstOrDefault();

            if (additionalData != null)
            {
                var coll = additionalData.Element(c + "Collection")?.Elements();
                if (coll != null)
                {
                    foreach (var addit in coll)
                    {
                        var name = addit.Attribute("name").Value.Trim();
                        if (addit.Element(c + "Collection")?.Element(c + "Item")?.Attribute("name") != null) //For additional Data that has a stepped name, like Manufacturer.Name
                            name = name + "." + addit.Element(c + "Collection").Element(c + "Item").Attribute("name").Value.Trim();

                        string value = addit.Value.Trim();

                        //Create a format that server will handle. Could alternatively be put into api as an overload to AddAdditionalData that takes name and value.
                        XElement doc = XElement.Parse("<Prop xmlns=\"\" Name=\"" + name + "\">" +
                                "<Value>" + value + "</Value>" +
                            "</Prop>");

                        if (!string.IsNullOrEmpty(name))
                            uutReport.AddAdditionalData(name, doc);

                    }
                }
            }
            return uutReport;            
        }


        private UUTReport CreateReport(TDM api, ATML50.schema.TestResults testResults)
        {

            var uutDefinition = (ATML202.schema.ItemDescription)testResults.UUT.Item;
            var testProgram = (ATML202.schema.ItemDescription)testResults.TestProgram.Item;
            UUTReport uutReport = api.CreateUUTReport(
                string.IsNullOrEmpty(testResults.Personnel.SystemOperator.name) ? testResults.Personnel.SystemOperator.ID : testResults.Personnel.SystemOperator.name,
                uutDefinition.Identification.ModelName,
                uutDefinition.Identification.Version,
                testResults.UUT.SerialNumber,
                "10",
                testProgram.name,
                testProgram.version);
            uutReport.StartDateTime = testResults.ResultSet.startDateTime;
            //uutReport.ReportId = new Guid(testResults.uuid); //Not important, use a new one
            var testStation = (ATML202.schema.ItemDescription)testResults.TestStation.Item;
            uutReport.StationName = testResults.TestStation.SerialNumber;
            return uutReport;
        }

        internal enum iconNames
        {
            Label = 0,
            Action = 1,
            Goto = 2,
            NI_FTPFiles = 3,
            NI_Flow_If = 4,
            NI_Flow_ElseIf = 5,
            NI_Flow_Else = 6,
            NI_Flow_End = 7,
            NI_Flow_For = 8,
            NI_Flow_ForEach = 9,
            NI_Flow_Break = 10,
            NI_Flow_Continue = 11,
            NI_Flow_DoWhile = 12,
            NI_Flow_While = 13,
            NI_Flow_Select = 14,
            NI_Flow_Case = 15,
            NI_Lock = 16,
            NI_Rendezvous = 17,
            NI_Queue = 18,
            NI_Notification = 19,
            NI_Wait = 20,
            NI_Batch_Sync = 21,
            NI_AutoSchedule = 22,
            NI_UseResource = 23,
            NI_ThreadPriority = 24,
            NI_Semaphore = 25,
            NI_BatchSpec = 26,
            NI_OpenDatabase = 27,
            NI_OpenSQLStatement = 28,
            NI_CloseSQLStatement = 29,
            NI_CloseDatabase = 30,
            NI_DataOperation = 31,
            NI_IVIDmm = 32,
            NI_IVIScope = 33,
            NI_IVIFgen = 34,
            NI_IVIPowerSupply = 35,
            NI_Switch = 36,
            NI_IVITools = 37,
            NI_LV_CheckSystemStatus = 38,
            NI_LV_RunVIAsynchronously = 39
        }
    }
}
