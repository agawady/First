using BusinessLayer.Context;
using BusinessLayer.Domains;
using Helper.Domains;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Services.ALSorayai
{
    class AttendenceService
    {
        private static string dbConnection;
        public static string DBConnection
        {
            get { return dbConnection; }
            set { dbConnection = value; }
        }

        private static string dbType;
        public static string DBType
        {
            get { return dbType; }
            set { dbType = value; }
        }

        private static TimeSpan maxShiftLength = new TimeSpan(12, 0, 0);


        public static ServiceResult<int> markAsMoved(DataRow transaction)
        {
            return markAsMoved(transaction, 'y');
        }

        public static ServiceResult<int> markAsMoved(DataRow transaction, char mark)
        {
            String setMoveFlagQuery = "UPDATE TRANSACTIONS SET IS_MOVED = '" + mark +"' " 
                            + "WHERE DEVICE_NAME = '" + transaction["DEVICE_NAME"].ToString() + "'"
                            + "AND USER_ID = '" + transaction["USER_ID"].ToString() + "'"
                            + "AND TRANS_TYPE = '" + transaction["TRANS_TYPE"].ToString() + "'"
                            + "AND TRANS_DATE = '" + transaction["TRANS_DATE"].ToString() + "';";
            return DAL.DAO.ExecuteNoneQuery(setMoveFlagQuery);
        }

        private static ServiceResult<DataTable> getMatchedTransactions(DateTime time, String empNumber)
        {
            String transDate = time.ToString("yyyy/MM/dd HH:mm:ss");
            return DAL.RemoteDAO.ExecuteQuery("SELECT * FROM ATIG_EMP_ATTENDANCE "
                + " WHERE TO_DATE('" + transDate + "', 'YYYY/MM/DD HH24:MI:SS') - "
                + " TO_DATE(TRANSACTION_DATE || ' ' || SHIFT_1_TIMEIN, 'DD-MM-YY HH24:MI')"
                + " between -0.583333 and 0.583333 AND EMPLOYEE_NUMBER = " + empNumber
                + " order by transaction_date"
                // + " AND SHIFT_1_TIMEOUT IS NULL "
                , dbConnection, dbType);
        }

        private static List<DateTime> mergeAndSort(DataRow period, DateTime? time)
        {
            List<DateTime> transactions = new List<DateTime> { };
            String date = DateTime.Parse(period["TRANSACTION_DATE"].ToString()).ToString("dd-MM-yy");
            
            DateTime transactionTime;
            if (DateTime.TryParseExact(date + " " + period["SHIFT_1_TIMEIN"], "dd-MM-yy HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out transactionTime))
                transactions.Add(transactionTime);
            if (DateTime.TryParseExact(date + " " + period["SHIFT_1_TIMEOUT"], "dd-MM-yy HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out transactionTime))
                transactions.Add(transactionTime);
            if (DateTime.TryParseExact(date + " " + period["BR_SHIFT_TIMEIN"], "dd-MM-yy HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out transactionTime))
                transactions.Add(transactionTime);
            if (DateTime.TryParseExact(date + " " + period["BR_SHIFT_TIMEOUT"], "dd-MM-yy HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out transactionTime))
                transactions.Add(transactionTime);

            if(time != null)
                transactions.Add(time.Value);
            transactions.Sort();
            return transactions;
        }


        private static Dictionary<string, string> orderTransactions(DataRow period, DateTime time)
        {
            TimeSpan brTimeSpan, totalTimeSpan;
            Dictionary<string, string> dict = new Dictionary<string, string>();

            List<DateTime> transactions = mergeAndSort(period, time);

            if (transactions.Count == 5) 
            {
                transactions[1] = transactions[2];
                transactions[2] = transactions[3];
                transactions[3] = transactions[4];
            }

            dict.Add("TRANSACTION_DATE", transactions[0].ToString("yyyy/MM/dd"));
            dict.Add("SHIFT_1_TIMEIN", transactions[0].ToString("HH:mm"));
            dict.Add("SHIFT_1_TIMEOUT", transactions[1].ToString("HH:mm"));
            totalTimeSpan = (transactions[1] - transactions[0]);
            dict.Add("TOTAL_DIFFR_TIME", totalTimeSpan.Hours + ":" + totalTimeSpan.Minutes);

            dict.Add("BR_SHIFT_TIMEIN", null);
            dict.Add("BR_SHIFT_TIMEOUT", null);
            dict.Add("BR_DIFFR_TIME", null);
            

            if (transactions.Count == 3)
            {
                dict["SHIFT_1_TIMEOUT"] = transactions[2].ToString("HH:mm");
                dict["BR_SHIFT_TIMEIN"] = transactions[1].ToString("HH:mm");
                totalTimeSpan = (transactions[2] - transactions[0]);
                dict["TOTAL_DIFFR_TIME"] = totalTimeSpan.Hours + ":" + totalTimeSpan.Minutes;
            }

            if (transactions.Count >= 4)
            {
                dict["BR_SHIFT_TIMEIN"] = transactions[2].ToString("HH:mm");
                dict["BR_SHIFT_TIMEOUT"] = transactions[3].ToString("HH:mm");               
                brTimeSpan = transactions[3]-transactions[2];
                totalTimeSpan = (transactions[1] - transactions[0]) + (transactions[3] - transactions[2]);
                dict["BR_DIFFR_TIME"] = brTimeSpan.Hours + ":" + brTimeSpan.Minutes;
                dict["TOTAL_DIFFR_TIME"] = totalTimeSpan.Hours + ":" + totalTimeSpan.Minutes;
            }

            return dict;
        }

        

        private static bool isRedundant(DataRow period, DateTime time)
        {
            List<DateTime> transactions = mergeAndSort(period, null);
            foreach (DateTime t in transactions)
            {
                if ((t - time).Duration() < new TimeSpan(0, 10, 0))
                    return true;
            }
            return false;
        }

        private static ServiceResult<String> matchTransaction(DataRow matched, DateTime time, String empNumber)
        {
            string transactionDate = DateTime.Parse(matched["TRANSACTION_DATE"].ToString()).ToString("dd-MM-yy");
            
            if(isRedundant(matched, time))
                return null;

            Dictionary<string, string> dict = orderTransactions(matched, time);
            
         
            return DAL.RemoteDAO.ExecuteNoneQuery("UPDATE ATIG_EMP_ATTENDANCE SET "
                + "  SHIFT_1_TIMEIN = '" + dict["SHIFT_1_TIMEIN"]
                + "', SHIFT_1_TIMEOUT = '" + dict["SHIFT_1_TIMEOUT"]
                + "', BR_SHIFT_TIMEIN = '" + dict["BR_SHIFT_TIMEIN"]
                + "', BR_SHIFT_TIMEOUT = '" + dict["BR_SHIFT_TIMEOUT"]
                + "', TRANSACTION_DATE = TO_DATE('" + dict["TRANSACTION_DATE"] + "','yyyy/MM/dd')"
                + ", BR_DIFFR_TIME = '" + dict["BR_DIFFR_TIME"]
                + "', TOTAL_DIFFR_TIME = '" + dict["TOTAL_DIFFR_TIME"]
                + "' WHERE TRANSACTION_DATE = TO_DATE('" + transactionDate + "','dd-MM-YY')"
                + " AND EMPLOYEE_NUMBER = " + empNumber, dbConnection, dbType);
               
        }

        private static void insertRecord(DateTime time, String empNumber, DataRow transaction)
        {
            ServiceResult<string> result = DAL.RemoteDAO.ExecuteNoneQuery("INSERT INTO ATIG_EMP_ATTENDANCE "
                + " (TRANSACTION_DATE, EMPLOYEE_NUMBER, SHIFT_1_TIMEIN)"
                + " VALUES (TO_DATE('" + time.ToString("yyyy/MM/dd") + "','yyyy/MM/dd'), "
                + empNumber + ", '" + time.ToString("HH:mm") + "')"
                , dbConnection, dbType);
            if (result.IsSuccess())
                markAsMoved(transaction);
            else if (result.ErrorMessage.englishValue.Contains("ORA-00001"))
                markAsMoved(transaction, 'e');
            else
                SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, result.ErrorMessage.englishValue, null);
        }

        public static void updateAttendence(DataRow transaction, string dbConnection, string dbType)
        {
            DBConnection = dbConnection;
            DBType = dbType;
            DateTime time = DateTime.Parse(transaction["TRANS_DATE"].ToString());
            String empNumber = transaction["USER_ID"].ToString();
            ServiceResult<DataTable> matched = getMatchedTransactions(time, empNumber);
            if (matched.IsSuccess())
            {
                if (matched.Result.Rows.Count > 0)
                {
                    ServiceResult<String> matchResult = matchTransaction(matched.Result.Rows[0], time, empNumber);
                    
                    if (matchResult == null || matchResult.IsSuccess())
                    {
                        markAsMoved(transaction);
                    }
                    else
                    {
                        SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, matchResult.ErrorMessage.englishValue, null);
                    }
                }
                else
                {
                    insertRecord(time, empNumber, transaction);                   
                }
            }
            else
            {
                SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, matched.ErrorMessage.englishValue, null);
            }
        }
    }
}
