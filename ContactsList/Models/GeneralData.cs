namespace ContactsList.Models
{
    using System.Collections.Generic;
        
    public struct Command
    {
        public const string BORROW = "BORROW";
        public const string RETURN = "RETURN";
        public const string CREATE = "CREATE";
        public const string REGISTER = "REGISTER";
        public const string VERIFY = "VERIFY";
        //CheckOut and CheckIn exchange function
        public const string pCHECKIN = "CHECKIN";
        public const string CHECKIN = "CheckIn";
        public const string pCHECKOUT = "CHECKOUT";
        public const string CHECKOUT = "CheckOut";
        public const string REGISTERINFO = "REGISTERINFO";
        public const string LOGUSAGE = "LOGUSAGE";
        public const string TOOLSLOG = "TOOLSLOG";
        public const string MODIFY = "MODIFY";        
    }

    public struct InfoItem
    {
        public const string Borrow = "Borrow ";
        public const string TimeIn = "TimeIn ";
        public const string TimeOut = "TimeOut";
    }

    public struct CommandResult
    {
        public const string OK = "LTS:OK";
        public const string NoLicense = "LTS:NoLicense";
        public const string EXPIRED = "LTS:Expired";
        public const string NotFound = "LTS:InvalidLicense";
        public const string LicenseUsed = "LTS:LicenseUsed";
        public const string InvalidVersion = "LTS:InvalidVersion";
        public const string InvalidCommand = "LTS:InvalidCommand";
        public const string IDRepeated = "LTS:LicenseIDRepeated";
        public const string NoRegister = "LTS:NotRegistered";
        public const string ErrorRegister = "LTS:ErrorRegister";
        public const string StorageException = "Exception";
    }
    
    //public struct ExtraEntityProperty
    //{
    //    public static Dictionary<string, string> DataItems = new Dictionary<string,string>();
    //}
}
