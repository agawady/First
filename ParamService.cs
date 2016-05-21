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
    public class ParamService
    {
        public static String getParameterValue(String paramName)
        {
            ServiceResult<DataTable> result = 
                DAL.DAO.ExecuteQuery("SELECT PARAM_VALUE FROM SYS_PARAMS WHERE PARAM_NAME = '" + paramName + "'");
            if (result.IsSuccess())
            {
                try
                {
                    String value = result.Result.Rows[0][0].ToString();
                    return value;
                }
                catch { }
            }
            return null;
        }

        public static ServiceResult<int> updateParameter(String paramName, String paramValue)
        {
            String updateCommand = "UPDATE SYS_PARAMS SET PARAM_VALUE = '" + paramValue +
                "' WHERE PARAM_NAME = '" + paramName + "'";

            ServiceResult<int> result = DAL.DAO.ExecuteNoneQuery(updateCommand);

            return result;
        }
    }
}
