using System.Collections.Generic;

namespace Errors
{
    static class ErrorCatagory
    {
        public const int CreateDisk = 1000;
        public const string CreateDiskDescription = "Failed to create disk";

        public const int CreateSecurityCertification = 2000;
        public const string CreateSecurityCertificationDescription = "Failed to create Security Certification";

        public const int GetSCKFromSC = 3000;
        public const string GetSCKFromSCDescription = "Failed to get SCK from Security Certification";




        public static string ErrorMsg(string errorCataDes, string errorDes)
        {
            return errorCataDes + ": " + errorDes + ".";
        }
    }

    class ErrorDescription
    {
        public static Dictionary<int, string> Errors = new Dictionary<int, string>()
        {
            {ErrorCatagory.CreateDisk + 1, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Wrong FS version number") },
            {ErrorCatagory.CreateDisk + 2, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Failed to create disk: Wrong FS version number") },
            {ErrorCatagory.CreateDisk + 3, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Disk capacity doesn't make integer number of blocks") },
            {ErrorCatagory.CreateDisk + 4, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Disk capacity doesn't make at least 10 blocks") },
            {ErrorCatagory.CreateDisk + 5, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Disk name length is greater than 256 Bytes") },
            {ErrorCatagory.CreateDisk + 6, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Disk name contains invalid character(s)") },
            {ErrorCatagory.CreateDisk + 7, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Invalid Security Certification Kernel") },
            {ErrorCatagory.CreateDisk + 8, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Cannot create new file") },
            {ErrorCatagory.CreateDisk + 9, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateDiskDescription, "Cannot write data") },


            {ErrorCatagory.CreateSecurityCertification + 1, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateSecurityCertificationDescription, "Invalid Security Certification Kernel") },
            {ErrorCatagory.CreateSecurityCertification + 2, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateSecurityCertificationDescription, "Cannot create new file") },
            {ErrorCatagory.CreateSecurityCertification + 3, ErrorCatagory.ErrorMsg(ErrorCatagory.CreateSecurityCertificationDescription, "Cannot write data") },

            {ErrorCatagory.GetSCKFromSC + 1, ErrorCatagory.ErrorMsg(ErrorCatagory.GetSCKFromSCDescription, "Cannot open SC file") },
            {ErrorCatagory.GetSCKFromSC + 2, ErrorCatagory.ErrorMsg(ErrorCatagory.GetSCKFromSCDescription, "Invalid SC file") },
            {ErrorCatagory.GetSCKFromSC + 3, ErrorCatagory.ErrorMsg(ErrorCatagory.GetSCKFromSCDescription, "Cannot read SC file") },
            

        };
    }

}
