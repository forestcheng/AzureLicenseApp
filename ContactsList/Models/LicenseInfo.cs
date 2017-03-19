using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ContactsList.Models
{
    using Microsoft.WindowsAzure.Storage.Table;
    
    public class UseInfo
    {
        public List<string> UseInfoList { get; set; }
    }

    public class LicenseInfo : TableEntity
    {
        // Your entity type must expose a parameter-less constructor
        public LicenseInfo() : base() { }

        // The partition key is a unique identifier for the partition within a given table.
        // The row key is a unique identifier for an entity within a given partition.
        public LicenseInfo(string software, string id) : base(software, id)       {        }

        public int Keys { get; set; }
        public string SoftVersion { get; set; }
        public DateTime MaintenanceDate { get; set; }
        public DateTime ExpireDate { get; set; }    // MaintenanceDate + 2 months
        public DateTime IssueDate { get; set; }
        // public DateTime BorrowDate { get; set; }
        public string Customer { get; set; }
        public string Contact { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }

        // computer MacAddress and users list        
        public string UseInfo { get; set; }
        
        /*
        public override IDictionary<string, EntityProperty> WriteEntity(Microsoft.WindowsAzure.Storage.OperationContext operationContext)
        {
            var results = base.WriteEntity(operationContext);
            foreach (var mu in ExtraEntityProperty.DataItems)
            {
                results.Add("M_" + mu.Key, new EntityProperty(mu.Value));
            }
            return results;
        }

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, Microsoft.WindowsAzure.Storage.OperationContext operationContext)
        {
            base.ReadEntity(properties, operationContext);
            //MacUsers = new List<string>();
            ExtraEntityProperty.DataItems = new Dictionary<string, string>();

            foreach (var item in properties)
            {
                switch (item.Key)
                {
                    //case "Timestamp":
                    //    Timestamp = item.Value.DateTimeOffsetValue.Value;
                    //    break;

                    //case "RowKey":
                    //    RowKey = item.Value.StringValue;
                    //    break;

                    //case "PartitionKey":
                    //    PartitionKey = item.Value.StringValue;
                    //    break;

                    case "Keys":
                        Keys = item.Value.Int32Value.Value;
                        break;

                    case "SoftVersion":
                        SoftVersion = item.Value.StringValue;
                        break;

                    case "ExpireDate":
                        ExpireDate = item.Value.DateTime.Value;
                        break;

                    default:
                        if (item.Key.StartsWith("M_"))
                        {
                            string realKey = item.Key.Substring(2);
                            ExtraEntityProperty.DataItems[realKey] = item.Value.StringValue;
                            //MacUsers.Add(item.Value.StringValue);
                        }
                        break;
                }
            }
        }
         * */
        
    }
}