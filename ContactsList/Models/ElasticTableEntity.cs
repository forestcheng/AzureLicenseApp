using System;
using System.Collections.Generic;
using System.Dynamic;

//using System.Linq;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace ContactsList.Models
{
    // http://pascallaurin42.blogspot.ca/2013/03/using-azure-table-storage-with-dynamic.html
    // http://azurestorageexplorer.codeplex.com/

    public class ElasticTableEntity : DynamicObject, ITableEntity
    {
        public ElasticTableEntity()
        {
            this.Properties = new Dictionary<string, EntityProperty>();
        }

        public String Value(String name)
        {
            switch (name)
            {
                case "PartitionKey":
                    return this.PartitionKey;
                case "RowKey":
                    return this.RowKey;
                default:
                    return this.Properties[name].StringValue;
            }
        }

        public IDictionary<string, EntityProperty> Properties { get; private set; }

        public object this[string key]
        {
            get
            {
                if (!this.Properties.ContainsKey(key))
                    this.Properties.Add(key, this.GetEntityProperty(key, null));

                return this.Properties[key];
            }
            set
            {
                var property = this.GetEntityProperty(key, value);

                if (this.Properties.ContainsKey(key))
                    this.Properties[key] = property;
                else
                    this.Properties.Add(key, property);
            }
        }

        #region DynamicObject overrides

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = this[binder.Name];
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            this[binder.Name] = value;
            return true;
        }

        #endregion

        #region ITableEntity implementation

        public string PartitionKey { get; set; }

        public string RowKey { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string ETag { get; set; }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            this.Properties = properties;
        }

        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            return this.Properties;
        }

        #endregion

        private EntityProperty GetEntityProperty(string key, object value)
        {
            if (value == null) return new EntityProperty((string)null);
            if (value.GetType() == typeof(byte[])) return new EntityProperty((byte[])value);
            if (value.GetType() == typeof(bool)) return new EntityProperty((bool)value);
            if (value.GetType() == typeof(DateTimeOffset)) return new EntityProperty((DateTimeOffset)value);
            if (value.GetType() == typeof(DateTime)) return new EntityProperty((DateTime)value);
            if (value.GetType() == typeof(double)) return new EntityProperty((double)value);
            if (value.GetType() == typeof(Guid)) return new EntityProperty((Guid)value);
            if (value.GetType() == typeof(int)) return new EntityProperty((int)value);
            if (value.GetType() == typeof(long)) return new EntityProperty((long)value);
            if (value.GetType() == typeof(string)) return new EntityProperty((string)value);
            throw new Exception("not supported " + value.GetType() + " for " + key);
        }

        private Type GetType(EdmType edmType)
        {
            switch (edmType)
            {
                case EdmType.Binary: return typeof(byte[]);
                case EdmType.Boolean: return typeof(bool);
                case EdmType.DateTime: return typeof(DateTime);
                case EdmType.Double: return typeof(double);
                case EdmType.Guid: return typeof(Guid);
                case EdmType.Int32: return typeof(int);
                case EdmType.Int64: return typeof(long);
                case EdmType.String: return typeof(string);
                default: throw new Exception("not supported " + edmType);
            }
        }

        private object GetValue(EntityProperty property)
        {
            switch (property.PropertyType)
            {
                case EdmType.Binary: return property.BinaryValue;
                case EdmType.Boolean: return property.BooleanValue;
                case EdmType.DateTime: return property.DateTimeOffsetValue;
                case EdmType.Double: return property.DoubleValue;
                case EdmType.Guid: return property.GuidValue;
                case EdmType.Int32: return property.Int32Value;
                case EdmType.Int64: return property.Int64Value;
                case EdmType.String: return property.StringValue;
                default: throw new Exception("not supported " + property.PropertyType);
            }
        }
    }

    /*
     // dynamic keyword to use a dynamic entity
            dynamic entity = new ElasticTableEntity();
            entity.PartitionKey = "Partition123";
            entity.RowKey = (DateTime.MaxValue.Ticks - DateTime.Now.Ticks).ToString();
            entity.Name = "Pascal";
            entity.Number = 34;
            entity.Bool = false;
            entity.Date = new DateTime(1912, 3, 4);
            entity.TokenId = Guid.NewGuid();
            entity["LastName"] = "Laurin";

            // Insert the entity we created dynamically
            table.Execute(TableOperation.Insert(entity));

            // Define the query, and select only the Email property.
            TableQuery<DynamicTableEntity> projectionQuery = new TableQuery<DynamicTableEntity>().Select(new string[] { "Email" });

                 // Define the query, and select only the Email property.
            TableQuery<DynamicTableEntity> projectionQuery = new TableQuery<DynamicTableEntity>().Select(new string[] { "Email" });

            // Define an entity resolver to work with the entity after retrieval.
            EntityResolver<string> resolver = (pk, rk, ts, props, etag) => props.ContainsKey("Email") ? props["Email"].StringValue : null;

            foreach (string projectedEmail in table.ExecuteQuery(projectionQuery, resolver, null, null))
            {
                Console.WriteLine(projectedEmail);
            }
     
     * 
     * */

}
