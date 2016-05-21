using BusinessLayer.Domains;
using Helper.Domains;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Services
{
    public class MappingService
    {
        public static ServiceResult<DataTable> getMappingInfo()
        {
            return DAL.DAO.ExecuteQuery("SELECT TOP 1 * FROM MAPPING");
        }

        public static ServiceResult<int> updateMapping(Mapping mapping)
        {
            String updateCommand = "UPDATE MAPPING SET DB_TYPE = '" + mapping.ConnectionProvider
                + "',CONNECTION_STRING = '" + mapping.ConnectionString + "',TRANS_TABLE = '" + mapping.TableName
                + "',USER_ID_COL = '" + mapping.UserIDColumnName + "',TIME_COL = '" + mapping.TimeColumnName
                + "',TIME_FORMAT = '" + mapping.TimeFormatColumnName + "',TRANS_TYPE_COL = '"
                + mapping.TransactionTypeColumnName + "',DEVICE_NAME_COL = '" + mapping.DeviceColumnName + "';";

            ServiceResult<int> result = DAL.DAO.ExecuteNoneQuery(updateCommand);

            return result;
        }
    }
}
