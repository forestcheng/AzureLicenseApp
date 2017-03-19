using ContactsList.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Security.Cryptography;
using System.Globalization;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;

using Newtonsoft.Json;

namespace ContactsList.Controllers
{

    public class ContactsController : ApiController
    {
        internal const string TableName = "licenses";
        internal const string LogName = "LicenseLog";
        internal const string DieLog = "DYNMIKDieLog";
        internal const string MoldLog = "DYNMIKMoldLog";
        internal const string LTSToolsLog = "LTSToolsLog";

        static int maxInterval = 3600 + 200; // seconds of Max interval of CheckIn + Extral 200s
        static CultureInfo ci = CultureInfo.CreateSpecificCulture("en-US");

        // Retrieve storage account information from connection string.
        //CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString);
        //static StorageCredentials sc = new StorageCredentials("licforest", "I/9+BOnBavrTDWclxwt0BzwsDaXE6rfdX+xHHlIjcT0i3XovyclbAnyOoYGcLa45eoH/acERFZOan0V4MmtgPQ==");
        static StorageCredentials sc = new StorageCredentials("ltslicensing", "7tyYrGwSfUUT1npdHSHvXawK9O8z8nIlB5LF1TRXLYUmriWuk2R9hgInNTYZ8GhcwmoS2QmQQSaPz/9cpnAnYw==");
        static CloudStorageAccount storageAccount = new CloudStorageAccount(sc, true);

        // Create a table client for interacting with the table service
        static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        // Create a table client for interacting with the table service 
        static CloudTable table = tableClient.GetTableReference(TableName);
        static CloudTable tableLog = tableClient.GetTableReference(LogName);
        static CloudTable tableDieLog = tableClient.GetTableReference(DieLog);
        static CloudTable tableMoldLog = tableClient.GetTableReference(MoldLog);
        static CloudTable tableToolsLog = tableClient.GetTableReference(LTSToolsLog);

        //Contact[] contacts = new Contact[] {
        //        new Contact { Id = 1, EmailAddress = "barney2@contoso.com", Name = "Barney Poland"},
        //        new Contact { Id = 2, EmailAddress = "lacy2@contoso.com", Name = "Lacy Barrera"},
        //    };

        DateTime nowEST;

        [HttpGet]
        public IEnumerable<Contact> Get() {
            return new Contact[]{
                new Contact { Id = 1, EmailAddress = "barney@contoso.com", Name = "Barney Poland"},
                new Contact { Id = 2, EmailAddress = "lacy@contoso.com", Name = "Lacy Barrera"},
            };
        }

        // GET api/values/5

        public IHttpActionResult Get(int id, string userLic) {
            string result = "";

            try {
                table.CreateIfNotExists();
                tableLog.CreateIfNotExists();
                tableDieLog.CreateIfNotExists();
                tableToolsLog.CreateIfNotExists();
            } catch (StorageException e) {
                result = e.Message + " " + userLic;
                return Ok(result);
            }

            // TimeZone localZone = TimeZone.CurrentTimeZone;
            try {
                TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                nowEST = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
            } catch {
                nowEST = DateTime.UtcNow;
            }

            var idx = userLic.IndexOf('=');
            if (idx < 0) {
                result = CommandResult.NoLicense;
                return Ok(result);
            }
            var command = userLic.Substring(0, idx);
            var commands = userLic.Substring(idx + 1).Split(';');

            // decode command
            byte[] licByte;
            string iLic, userInfo;

            try {
                licByte = Convert.FromBase64String(commands[0]);
                System.Text.Encoding ascii = System.Text.Encoding.ASCII;
                iLic = ascii.GetString(licByte);

                licByte = Convert.FromBase64String(iLic);
                iLic = ascii.GetString(licByte);

                // decode Mac;User
                licByte = Convert.FromBase64String(commands[1]);
                userInfo = ascii.GetString(licByte);
            } catch (Exception e) {
                LogInfo(command, userLic, e.Message, "Exception!");
                string resp = e.Message;
                resp = Regex.Replace(resp, "base", " ", RegexOptions.IgnoreCase);
                resp = Regex.Replace(resp, "64", " ");
                return Ok(resp);
            }

            switch (command) {
                case Command.BORROW:
                    result = BorrowLicense(iLic + ";" + userInfo);
                    break;

                case Command.RETURN:
                    result = ReturnLicense(iLic + ";" + userInfo);
                    break;

                case Command.CREATE:
                    result = CreateLicense(iLic + ";" + userInfo);
                    break;

                case Command.REGISTER:
                    result = RegisterLicense(iLic + ";" + userInfo);
                    break;

                case Command.VERIFY:
                case Command.CHECKOUT:
                    string infoStr = iLic + ";" + userInfo;
                    result = VerifyLicense(infoStr);

                    var lics = infoStr.Split(';');
                    if (lics[1] == "DYNMIK Die" || lics[1] == "DYNMIK Mold") {
                        // lic: [3]DomainName;[4]UserName;[5]MachineName;[6]OS;[7]userSoft;[8]userVersion
                        string note = "";
                        if (!result.Contains(CommandResult.OK))
                            note = result.Replace(";", "_");
                        LogUsage(lics[0] + ";" + lics[1] + ";" + lics[2] + ";App;Startup; ;" + note + ";" + lics[4] + ";" + lics[8]);
                    }

                    break;

                case Command.pCHECKIN:
                    // ensure to CheckIn, try 6 times
                    for (int i = 0; i < 6; ++i) {
                        result = CheckInLicense(iLic + ";" + userInfo);
                        if (result == CommandResult.StorageException)
                            continue;
                        else
                            break;
                    }

                    break;

                case Command.CHECKIN:
                case Command.pCHECKOUT:
                    result = CheckOutLicense(iLic + ";" + userInfo);
                    break;

                case Command.MODIFY:
                    result = ModifyLicense(iLic + ";" + userInfo);
                    break;

                case Command.REGISTERINFO:
                    result = RegisterInfo(iLic + ";" + userInfo);
                    break;

                case Command.LOGUSAGE:
                    LogUsage(iLic + ";" + userInfo);
                    break;

                case Command.TOOLSLOG:
                    ToolsLog(iLic + ";" + userInfo);
                    break;

                default:
                    result = CommandResult.NotFound;
                    break;
            }


            // string ip = System.Web.HttpContext.Current.Request.UserHostAddress;
            if (command != Command.LOGUSAGE && command != Command.TOOLSLOG)
                LogInfo(command, iLic, userInfo, result);

            return Ok(result);
            //var c = contacts.FirstOrDefault((p) => p.Id == id);
            ////Contact c = new Contact { Id = 1, EmailAddress = "barney@contoso.com", Name = "Barney Poland" }; //contacts[1];
            //c.Name += result;
            //var n = NotFound();
            //if (c == null) return NotFound();
            //return Ok(c);
        }
        private void ToolsLog(string cmdInfo) {
            // cmdInfo: [0]Mac;[1]Software;[2]Version;
            // cmdInfo: [3]DomainName;[4]UserName;[5]MachineName;[6]OSVersion;//v1.2.2[7]Command
            var infos = cmdInfo.Split(';');
            ElasticTableEntity entity = new ElasticTableEntity();
            entity.PartitionKey = nowEST.ToString("yyyyMMMdd", ci);
            entity.RowKey = nowEST.ToString("HH:mm:ss.f", ci);
            entity["Software"] = infos[1];
            entity["Version"] = infos[2];
            entity["DomainName"] = infos[3];
            entity["UserName"] = infos[4];
            entity["MachineName"] = infos[5];
            entity["OSVersion"] = infos[6];
            entity["Mac"] = infos[0];
            if (infos.Length > 7) entity["Command"] = infos[7];

            tableToolsLog.Execute(TableOperation.Insert(entity));
        }

        private void LogUsage(string cmdInfo) {
            // cmdInfo: [0]Mac;[1]Software;[2]" ";
            // cmdInfo: [3]Component;[4]Command;[5]Parameters;[6]Note;[7]Username;[8]Version
            var infos = cmdInfo.Split(';');
            ElasticTableEntity entity = new ElasticTableEntity();
            entity.PartitionKey = nowEST.ToString("yyyyMMMdd", ci);
            entity.RowKey = nowEST.ToString("HH:mm:ss.f", ci);
            entity["Component"] = infos[3];
            entity["Command"] = infos[4];
            entity["Parameters"] = infos[5];
            entity["Note"] = infos[6];
            entity["Mac"] = infos[0];
            entity["Username"] = infos[7];
            entity["Version"] = infos[8];

            switch (infos[1].Replace(" ", "") + "Log") {
                case DieLog:
                    tableDieLog.Execute(TableOperation.Insert(entity));
                    break;

                case MoldLog:
                    tableMoldLog.Execute(TableOperation.Insert(entity));
                    break;
            }
        }

        private void LogInfo(string command, string license, string userInfo, string result) {
            dynamic entity = new ElasticTableEntity();
            entity.PartitionKey = nowEST.ToString("yyyyMMM", ci);
            entity.RowKey = nowEST.ToString("dd HH:mm:ss.f", ci);  //(DateTime.MaxValue.Ticks - DateTime.Now.Ticks).ToString();
            entity["Command"] = command;
            entity["License"] = license;
            entity["UserInfo"] = userInfo;
            entity["Result"] = result;

            // Insert the entity we created dynamically
            tableLog.Execute(TableOperation.Insert(entity));
        }

        private string BorrowLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]Mac;
            // lic: [3]DomainName;[4]UserName;[5]BorrowDays
            // version 1.2
            // lic: [6]CPUID

            var lics = lic.Split(';');
            if (lics.Length < 6) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);

            if (licInfo == null) {
                result = CommandResult.NotFound;
                return result;
            }

            // check expireDate
            TimeSpan span = licInfo.ExpireDate - nowEST;
            if (span.TotalDays < 0) {
                result = CommandResult.EXPIRED;
                return result;
            }

            UseInfo useInfos = null;
            if (licInfo.UseInfo == null || licInfo.UseInfo == "") {
                useInfos = new UseInfo();
                useInfos.UseInfoList = new List<string>();
            } else {
                useInfos = JsonConvert.DeserializeObject<UseInfo>(licInfo.UseInfo);
            }

            // Mac;DomainName;UserName
            string iUseInfo = lics[2] + ";" + lics[3] + ";" + lics[4];
            bool isRegistered = false;
            int days = Convert.ToInt32(lics[5]);
            int tDay = Convert.ToInt32(span.TotalDays);
            string borrowDate = nowEST.AddDays((days < tDay) ? days : tDay).ToString("yyyyMMMdd", ci);

            for (int i = 0; i < useInfos.UseInfoList.Count; ++i) {
                if (useInfos.UseInfoList[i].IndexOf(iUseInfo) >= 0) {
                    useInfos.UseInfoList[i] = iUseInfo + ";" + InfoItem.Borrow + borrowDate;
                    licInfo.UseInfo = JsonConvert.SerializeObject(useInfos, Formatting.Indented);

                    try {
                        // https://azure.microsoft.com/en-us/blog/managing-concurrency-in-microsoft-azure-storage-2/
                        TableOperation mergeOperation = TableOperation.Merge(licInfo);
                        table.Execute(mergeOperation);
                    } catch (StorageException) {
                        result = "Wrong with Borrowing License, Please try it again.";
                        return result;
                    }

                    isRegistered = true;
                    break;
                }
            }

            if (isRegistered) {
                System.Text.Encoding ascii = System.Text.Encoding.ASCII;
                result += ";" + Convert.ToBase64String(ascii.GetBytes(borrowDate));
                // encrypt iUseInfo + cpuID
                using (MD5 md5Hash = MD5.Create()) {
                    result += ";" + GetMd5Hash(md5Hash, lics[2]);
                    if (lics.Length > 5) {
                        result += ";" + GetMd5Hash(md5Hash, lics[6]);
                    }
                }
            } else {
                result = CommandResult.LicenseUsed + ";FULL";
            }

            return result;
        }

        private string ReturnLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]Mac;
            // lic: [3]DomainName;[4]UserName

            var lics = lic.Split(';');
            if (lics.Length < 5) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);
            if (licInfo == null) {
                result = CommandResult.NotFound;
                return result;
            }

            UseInfo useInfos = null;
            if (licInfo.UseInfo == null) {
                return result;
            } else {
                useInfos = JsonConvert.DeserializeObject<UseInfo>(licInfo.UseInfo);
            }

            // Mac;DomainName;UserName
            string iUseInfo = lics[2] + ";" + lics[3] + ";" + lics[4];
            for (int i = 0; i < useInfos.UseInfoList.Count; ++i) {
                if (useInfos.UseInfoList[i].IndexOf(iUseInfo) >= 0) {
                    useInfos.UseInfoList[i] = iUseInfo;
                    licInfo.UseInfo = JsonConvert.SerializeObject(useInfos, Formatting.Indented);

                    try {
                        // https://azure.microsoft.com/en-us/blog/managing-concurrency-in-microsoft-azure-storage-2/
                        TableOperation mergeOperation = TableOperation.Merge(licInfo);
                        table.Execute(mergeOperation);
                    } catch (StorageException) {
                        result = "Wrong with Return License, Please try it again.";
                        return result;
                    }

                    break;
                }
            }

            return result;
        }

        private string CreateLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]Version;[3]MaintenanceDate;[4]ExpireDate;[5]IssueDate;[6]Keys;
            // lic: [7]Customer;[8]Contact;[9]Email;[10]Phone;[11]Host;

            var lics = lic.Split(';');
            if (lics.Length < 2) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);
            if (licInfo == null) {
                licInfo = new LicenseInfo(lics[1], lics[0]);

                licInfo.Customer = licInfo.Contact = licInfo.Email = licInfo.Phone = "";

                UseInfo useInfos = new UseInfo();
                useInfos.UseInfoList = new List<string>();
                licInfo.UseInfo = JsonConvert.SerializeObject(useInfos, Formatting.Indented);
            }
            //else
            //{
            //    result = CommandResult.IDRepeated;
            //    return result;
            //}
            licInfo.SoftVersion = lics[2];
            licInfo.MaintenanceDate = new DateTime(Int32.Parse(lics[3].Substring(0, 4)), Int32.Parse(lics[3].Substring(4, 2)), Int32.Parse(lics[3].Substring(6, 2)));
            licInfo.ExpireDate = new DateTime(Int32.Parse(lics[4].Substring(0, 4)), Int32.Parse(lics[4].Substring(4, 2)), Int32.Parse(lics[4].Substring(6, 2)));
            licInfo.IssueDate = new DateTime(Int32.Parse(lics[5].Substring(0, 4)), Int32.Parse(lics[5].Substring(4, 2)), Int32.Parse(lics[5].Substring(6, 2)));
            licInfo.Keys = Int32.Parse(lics[6]);

            if (lics.Length > 10) {
                licInfo.Customer = lics[7];
                licInfo.Contact = lics[8];
                licInfo.Email = lics[9];
                licInfo.Phone = lics[10];
            }

            // Create the InsertOrReplace  TableOperation
            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(licInfo);
            // Execute the operation.
            table.Execute(insertOrMergeOperation);

            return result;
        }

        private string VerifyLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]SoftVersion;[2]Mac;
            // lic: [3]DomainName;[4]UserName;[5]MachineName;[6]OS;[7]userSoft;[8]userVersion

            var lics = lic.Split(';');

            if (lics.Length < 9) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            LicenseInfo licInfo = null;
            if (lics[1].Contains(lics[7] + " " + lics[8])) {   // Package License
                licInfo = RetrieveEntityUsingPointQuery(table, "Package", lics[0]);
            } else {   // use userSoft = lics[7] check software name - SerialID
                licInfo = RetrieveEntityUsingPointQuery(table, lics[7], lics[0]);
            }

            if (licInfo == null) {
                result = CommandResult.NotFound;
                return result;
            }

            // check userVersion
            if (!licInfo.SoftVersion.Contains(lics[8])) {
                result = CommandResult.InvalidVersion;
                return result;
            }

            // check expireDate
            TimeSpan span = licInfo.ExpireDate - nowEST;
            if (span.TotalDays < 0) {
                result = CommandResult.EXPIRED;
                return result;
            } else {
                System.Text.Encoding ascii = System.Text.Encoding.ASCII;
                int tDay = Convert.ToInt32(span.TotalDays);
                result += ";" + Convert.ToBase64String(ascii.GetBytes(tDay.ToString()));   //Convert.ToString(span.TotalDays)));                
            }

            UseInfo useInfos = null;
            if (licInfo.UseInfo == null || licInfo.UseInfo == "") {
                useInfos = new UseInfo();
                useInfos.UseInfoList = new List<string>();
            } else {
                useInfos = JsonConvert.DeserializeObject<UseInfo>(licInfo.UseInfo);
            }

            // Mac;DomainName;UserName
            string iUseInfo = lics[2] + ";" + lics[3] + ";" + lics[4];

            bool isRegistered = false;
            for (int i = 0; i < useInfos.UseInfoList.Count; ++i) {
                if (useInfos.UseInfoList[i].IndexOf(iUseInfo) >= 0) {
                    int idx = useInfos.UseInfoList[i].IndexOf(InfoItem.Borrow);
                    if (idx >= 0) {
                        var borrowStr = useInfos.UseInfoList[i].Substring(idx + InfoItem.Borrow.Length);
                        DateTime borrowDate = DateTime.ParseExact(borrowStr, "yyyyMMMdd", ci);
                        span = borrowDate - nowEST;
                        if (span.TotalDays < 0) {
                            useInfos.UseInfoList[i] = iUseInfo + ";" + InfoItem.TimeIn + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci);
                        }
                    } else {
                        useInfos.UseInfoList[i] = iUseInfo + ";" + InfoItem.TimeIn + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci);
                    }
                    isRegistered = true;
                    break;
                }
            }

            if (!isRegistered) {
                /*
                if (useInfos.UseInfoList.Count == licInfo.Keys) {
                    result = CommandResult.LicenseUsed + ";FULL";
                    return result;
                } else if (licInfo.PartitionKey.StartsWith("DYNMIK")) {
                    useInfos.UseInfoList.Add(iUseInfo + ";" + InfoItem.TimeIn + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci));
                } else {
                    result = CommandResult.NoRegister;
                    return result;
                }
                */

                // Check Domain
                /*
                if (useInfos.UseInfoList.Count > 0) {
                    var domains = useInfos.UseInfoList[0].Split(';');
                    if (domains.Length > 2) {
                        var domain = domains[1];
                        var ds = domain.Split('.');
                        var dsUser = lics[3].Split('.');

                        if (ds.Length > 1) {
                            if (dsUser.Length > 1) {
                                int nDomain = 1;
                                if (ds.Length > 2) nDomain = 2;

                                for (int i = 0; i < nDomain; ++i) {
                                    if (String.Compare(ds[ds.Length - i], dsUser[dsUser.Length - i], true) != 0) {
                                        result = CommandResult.ErrorRegister;
                                        return result;
                                    }
                                }
                            } else {
                                result = CommandResult.ErrorRegister;
                                return result;
                            }

                        }
                    }
                }
                */

                if (useInfos.UseInfoList.Count < licInfo.Keys) {
                    useInfos.UseInfoList.Add(iUseInfo + ";" + InfoItem.TimeIn + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci));
                } else {
                    bool isCheckedIn = false;
                    for (int i = 0; i < useInfos.UseInfoList.Count; ++i) {
                        var iInfo = useInfos.UseInfoList[i];
                        int idx = iInfo.IndexOf(InfoItem.TimeIn);
                        if (idx < 0) {
                            useInfos.UseInfoList[i] = iUseInfo + ";" + InfoItem.TimeIn + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci);
                            isCheckedIn = true;
                            break;
                        } else {
                            var timeInStr = iInfo.Substring(idx + InfoItem.TimeIn.Length);
                            DateTime timeIn = DateTime.ParseExact(timeInStr, "yyyyMMMdd HH:mm:ss", ci);
                            span = nowEST - timeIn;
                            if (span.TotalSeconds > maxInterval) {
                                useInfos.UseInfoList[i] = iUseInfo + ";" + InfoItem.TimeIn + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci);
                                isCheckedIn = true;
                                break;
                            }
                        }
                    }
                    if (!isCheckedIn) {
                        result = CommandResult.LicenseUsed + ";FULL";
                        return result;
                    }

                }
            }
            useInfos.UseInfoList = useInfos.UseInfoList.OrderBy(x => x.Length).ToList();

            licInfo.UseInfo = JsonConvert.SerializeObject(useInfos, Formatting.Indented);

            try {
                // https://azure.microsoft.com/en-us/blog/managing-concurrency-in-microsoft-azure-storage-2/
                TableOperation mergeOperation = TableOperation.Merge(licInfo);
                table.Execute(mergeOperation);
            } catch (StorageException ex) {
                result = "Please run the program again or contact Longterm Services with: " + ex.Message;
            }

            return result;
        }

        private string CheckInLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]Mac;
            // lic: [3]DomainName;[4]UserName

            var lics = lic.Split(';');
            if (lics.Length < 5) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);

            if (licInfo == null) {
                result = CommandResult.NotFound;
                return result;
            }

            // check expireDate
            TimeSpan span = licInfo.ExpireDate - nowEST;
            if (span.TotalDays < 0) {
                result = CommandResult.EXPIRED;
                return result;
            }

            UseInfo useInfos = null;
            if (licInfo.UseInfo == null || licInfo.UseInfo == "") {
                useInfos = new UseInfo();
                useInfos.UseInfoList = new List<string>();
            } else {
                useInfos = JsonConvert.DeserializeObject<UseInfo>(licInfo.UseInfo);
            }

            // Mac;DomainName;UserName
            string iUseInfo = lics[2] + ";" + lics[3] + ";" + lics[4];

            bool isCheckedIn = false;
            for (int i = 0; i < useInfos.UseInfoList.Count; ++i) {
                if (useInfos.UseInfoList[i].Contains(iUseInfo)) {
                    if (useInfos.UseInfoList[i].Contains(InfoItem.TimeIn)) {
                        useInfos.UseInfoList[i] = iUseInfo + ";" + InfoItem.TimeIn + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci);
                    }
                    isCheckedIn = true;
                    break;
                }
            }

            if (!isCheckedIn) {
                result = CommandResult.LicenseUsed + ";FULL";
                return result;
            }

            licInfo.UseInfo = JsonConvert.SerializeObject(useInfos, Formatting.Indented);

            try {
                // https://azure.microsoft.com/en-us/blog/managing-concurrency-in-microsoft-azure-storage-2/
                TableOperation mergeOperation = TableOperation.Merge(licInfo);
                table.Execute(mergeOperation);
            } catch (StorageException) {
                result = CommandResult.StorageException;
            }

            return result;
        }

        private string CheckOutLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]Mac;
            // lic: [3]DomainName;[4]UserName

            var lics = lic.Split(';');
            if (lics.Length < 5) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);

            if (licInfo == null) {
                result = CommandResult.NotFound;
                return result;
            }

            UseInfo useInfos = null;
            if (licInfo.UseInfo == null || licInfo.UseInfo == "") {
                useInfos = new UseInfo();
                useInfos.UseInfoList = new List<string>();
            } else {
                useInfos = JsonConvert.DeserializeObject<UseInfo>(licInfo.UseInfo);
            }

            // Mac;DomainName;UserName
            string iUseInfo = lics[2] + ";" + lics[3] + ";" + lics[4];

            for (int i = 0; i < useInfos.UseInfoList.Count; ++i) {
                if (useInfos.UseInfoList[i].IndexOf(iUseInfo) >= 0 && (!useInfos.UseInfoList[i].Contains(InfoItem.Borrow))) {
                    //useInfos.UseInfoList.RemoveAt(i);
                    useInfos.UseInfoList[i] = iUseInfo + ";" + InfoItem.TimeOut + nowEST.ToString("yyyyMMMdd HH:mm:ss", ci);
                    break;
                }
            }

            licInfo.UseInfo = JsonConvert.SerializeObject(useInfos, Formatting.Indented);

            try {
                // https://azure.microsoft.com/en-us/blog/managing-concurrency-in-microsoft-azure-storage-2/
                TableOperation mergeOperation = TableOperation.Merge(licInfo);
                table.Execute(mergeOperation);
            } catch (StorageException) {
            }

            return result;
        }

        private string RegisterInfo(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]SoftVersion;[3]MaintenanceDate;[4]ExpireDate;[5]IssueDate;[6]Keys;
            var lics = lic.Split(';');
            if (lics.Length < 7) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);
            // TODO: validate lic[0] SerialID

            if (licInfo == null) {
                result = CommandResult.NotFound;
                return result;
            }
            // check userVersion
            if (lics[2] != licInfo.SoftVersion) {
                result = CommandResult.InvalidVersion;
                return result;
            }
            // check expireDate
            TimeSpan span = licInfo.ExpireDate - nowEST;
            if (span.TotalDays < 0) {
                result = CommandResult.EXPIRED;
                return result;
            }
            // check [6]Keys
            //int numKey = Int32.Parse(lics[6]);
            //if (numKey != licInfo.Keys) {
            //    result = CommandResult.LicenseUsed + ";NOT" + lics[6];
            //    return result;
            //}

            result += ";" + licInfo.Customer + ";" + licInfo.Contact + ";" + licInfo.Email + ";" + licInfo.Phone;
            return result;
        }

        private string RegisterLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]SoftVersion;[3]MaintenanceDate;[4]ExpireDate;[5]IssueDate;[6]Keys;
            // lic: [7]Mac;[8]DomainName;[9]Customer;[10]Contact;[11]Email;[12]Phone;[13]userSoft;[14]userVersion;[15]UserName

            var lics = lic.Split(';');
            if (lics.Length < 16) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            // use userSoft = lics[13] check software name
            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);
            // TODO: validate lic[0] SerialID

            if (licInfo == null) {
                result = CommandResult.NotFound;
                return result;
            }

            string softVersion = lics[1];
            // check userVersion
            if (lics[1] == "Package") {
                if (!lics[2].Contains(lics[13] + " " + lics[14])) {
                    result = CommandResult.NotFound;
                    return result;
                }
                softVersion = lics[2];
            } else {
                if (lics[14] != licInfo.SoftVersion) {
                    result = CommandResult.InvalidVersion;
                    return result;
                }
            }

            // check expireDate
            TimeSpan span = licInfo.ExpireDate - nowEST;
            if (span.TotalDays < 0) {
                result = CommandResult.EXPIRED;
                return result;
            }

            // check [6]Keys
            //int numKey = Int32.Parse(lics[6]);
            //if (numKey != licInfo.Keys) {
            //    result = CommandResult.LicenseUsed + ";NOT" + lics[6];
            //    return result;
            //}

            UseInfo useInfos = null;
            if (licInfo.UseInfo == null || licInfo.UseInfo == "") {
                useInfos = new UseInfo();
                useInfos.UseInfoList = new List<string>();
            } else {
                useInfos = JsonConvert.DeserializeObject<UseInfo>(licInfo.UseInfo);
            }

            // check DomainName
            if (useInfos.UseInfoList.Count > 0) {
                var parts = useInfos.UseInfoList[0].Split(';');
                string domainName = parts[1];
                string[] ds = domainName.ToLower().Split('.');
                string[] us = lics[8].ToLower().Split('.');
                if (ds.Length > 1) {
                    bool isFalse = false;
                    for (int i = 0; i < 2; ++i) {
                        if (ds[ds.Length - 1 - i] != us[us.Length - 1 - i]) {
                            isFalse = true;
                            break;
                        }
                    }
                    if (isFalse) {
                        result = CommandResult.ErrorRegister;
                        return result;
                    }

                }
            }

            //register customer info
            licInfo.Customer = lics[9];
            licInfo.Contact = lics[10];
            licInfo.Email = lics[11];
            licInfo.Phone = lics[12];

            // register Mac;DomainName;UserName
            string iUseInfo = lics[7] + ";" + lics[8] + ";" + lics[15];
            bool isRegistered = false;
            for (int i = 0; i < useInfos.UseInfoList.Count; ++i) {
                if (useInfos.UseInfoList[i].IndexOf(iUseInfo) >= 0) {
                    isRegistered = true;
                    break;
                }
            }
            if (!isRegistered) {
                if (useInfos.UseInfoList.Count < licInfo.Keys) {
                    useInfos.UseInfoList.Add(iUseInfo);
                    licInfo.UseInfo = JsonConvert.SerializeObject(useInfos, Formatting.Indented);
                }
                //Let VerifyLicense check if FULL
                //else {
                //    result = CommandResult.LicenseUsed + ";FULL";
                //    return result;
                //}
            }

            try {
                // https://azure.microsoft.com/en-us/blog/managing-concurrency-in-microsoft-azure-storage-2/
                TableOperation mergeOperation = TableOperation.Merge(licInfo);
                table.Execute(mergeOperation);
            } catch (StorageException) { }

            // return ID;Software;Mac - For safety reason, Double Base64 encode
            System.Text.Encoding ascii = System.Text.Encoding.ASCII;
            string base64 = Convert.ToBase64String(ascii.GetBytes(lics[0] + ";" + softVersion + ";" + lics[7]));
            base64 = Convert.ToBase64String(ascii.GetBytes(base64));
            result += ";" + base64;

            return result;
        }

        private string ModifyLicense(string lic) {
            string result = CommandResult.OK;

            // lic: [0]SerialID;[1]Software;[2]MacHash;
            // lic: [3]VariableName=Value;[4]VariableName=Value;[5]VariableName=Value;....

            var lics = lic.Split(';');
            if (lics.Length < 4) {
                result = CommandResult.InvalidCommand;
                return result;
            }

            // Query the table and retrieve a collection of entities.
            var query = new TableQuery<ElasticTableEntity>();
            IEnumerable<ElasticTableEntity> entities = null;
            IEnumerable<ElasticTableEntity> q = null;
            q = table.ExecuteQuery(query).Where(e => e.Value("PartitionKey").StartsWith(lics[1])).Select(e => e);
            entities = q.ToList();

            var md5Hash = MD5.Create();
            //string userHash = GetMd5Hash(md5Hash, lics[2]);
            ElasticTableEntity iEntity = null;
            foreach (ElasticTableEntity entity in entities) {
                string iHash = GetMd5Hash(md5Hash, entity.RowKey);
                if (iHash == lics[2]) {
                    iEntity = entity;
                    break;
                }

            }
            if (iEntity == null) {
                result = CommandResult.NotFound;
                return result;
            }
            for (int i = 3; i < lics.Length; ++i) {
                var nv = lics[i].Split('=');
                if (nv.Length == 2) {
                    if (nv[0].Contains("Date") && iEntity.Properties.ContainsKey(nv[0])) {
                        iEntity[nv[0]] = new DateTime(Int32.Parse(nv[1].Substring(0, 4)), Int32.Parse(nv[1].Substring(4, 2)), Int32.Parse(nv[1].Substring(6, 2))); ;
                    }
                }
            }
            table.Execute(TableOperation.Merge(iEntity));

            return result;

            /*
            LicenseInfo licInfo = RetrieveEntityUsingPointQuery(table, lics[1], lics[0]);

            if (licInfo == null)
            {
                result = CommandResult.NotFound;
                return result;
            }

            for (int i = 4; i < lics.Length; ++i)
            {
                var nv = lics[i].Split('=');
                if (nv.Length == 2)
                {
                    switch (nv[0])
                    {
                        case nameof(licInfo.Contact):
                            licInfo.Contact = nv[1];
                            break;

                        case nameof(licInfo.Customer):
                            licInfo.Customer = nv[1];
                            break;

                        case nameof(licInfo.Email):
                            licInfo.Email = nv[1];
                            break;

                        case nameof(licInfo.ExpireDate):
                            licInfo.ExpireDate = new DateTime(Int32.Parse(nv[1].Substring(0, 4)), Int32.Parse(nv[1].Substring(4, 2)), Int32.Parse(nv[1].Substring(6, 2)));
                            break;

                        case nameof(licInfo.IssueDate):
                            licInfo.IssueDate = new DateTime(Int32.Parse(nv[1].Substring(0, 4)), Int32.Parse(nv[1].Substring(4, 2)), Int32.Parse(nv[1].Substring(6, 2)));
                            break;

                        case nameof(licInfo.MaintenanceDate):
                            licInfo.MaintenanceDate = new DateTime(Int32.Parse(nv[1].Substring(0, 4)), Int32.Parse(nv[1].Substring(4, 2)), Int32.Parse(nv[1].Substring(6, 2)));
                            break;

                        case nameof(licInfo.Phone):
                            licInfo.Phone = nv[1];
                            break;

                        case nameof(licInfo.SoftVersion):
                            licInfo.SoftVersion = nv[1];
                            break;

                        case nameof(licInfo.Keys):
                            licInfo.Keys = Int32.Parse(nv[1]);
                            break;

                        default:
                            //result = CommandResult.NotFound;
                            break;
                    }

                }
            }

            try
            {
                // https://azure.microsoft.com/en-us/blog/managing-concurrency-in-microsoft-azure-storage-2/
                TableOperation mergeOperation = TableOperation.Merge(licInfo);
                table.Execute(mergeOperation);
            }
            catch (StorageException)
            {
            }
            */

        }


        private LicenseInfo RetrieveEntityUsingPointQuery(CloudTable table, string partitionKey, string rowKey) {
            TableOperation retrieveOp = TableOperation.Retrieve<LicenseInfo>(partitionKey, rowKey);
            TableResult result = table.Execute(retrieveOp);
            LicenseInfo lic = result.Result as LicenseInfo;
            return lic;
        }

        static string GetMd5Hash(MD5 md5Hash, string input) {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++) {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        /*        
        public IHttpActionResult Get(string s)
        {
            var c = new Contact { Id = 4, EmailAddress = "barn33@contoso.com" + s, Name = "Barney Poland" };
            return Ok(c);
        }
         * */
    }
}