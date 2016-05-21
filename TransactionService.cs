using BusinessLayer.Context;
using BusinessLayer.Domains;
using BusinessLayer.Services.ALSorayai;
using DAL;
using Helper.Domains;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BusinessLayer.Services
{
    public class TransactionService
    {
        public static bool IsRunning = true;
        public static bool terminated = false;
        public static int insertLog(List<LogRecord> transactionLog, String deviceID, String deviceName)
        {
            // 1. order the transactions by time descending 
            // 2. keep inserting transactions till we find the record already exists in transaction table

          //  List<LogRecord> SortedList = transactionLog.OrderByDescending(o => o.time).ToList();

            Dictionary<string, Dictionary<long, string>> dict = getDeviceTransactionsDict(int.Parse(deviceID));

            int Count = 0;
            foreach (LogRecord record in transactionLog)
            {
                if (dict.ContainsKey(record.sdwEnrollNumber))
                {
                    if (dict[record.sdwEnrollNumber].ContainsKey(record.time.Ticks))
                        continue;
                }
                ServiceResult<int> result = insertRecord(record, deviceID, deviceName);
                if (result.IsSuccess())
                    Count++;
                else
                {
                   // if (!result.ErrorMessage.errorCode.Equals("-2146232060"))
                    {
                        string[] paramlist = new string[4];
                        paramlist[0] = deviceName;
                        paramlist[1] = record.sdwEnrollNumber;
                        paramlist[2] = record.time.ToString();
                        paramlist[3] = record.idwInOutMode.ToString();

                        SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, result.ErrorMessage.englishValue, paramlist);
                    }
                    /*  else // commented on 27-4-2016
                      {
                          // record already exists from now on all records are duplicated and we should stop
                          //break; 
                      }*/
                }
            }
            return Count;
        }

        public static ServiceResult<int> insertRecord(LogRecord record, String DeviceID, String deviceName)
        {
            ServiceResult<int> serviceResult = DAL.DAO.ExecuteNoneQuery(
                "INSERT INTO TRANSACTIONS(DEVICE_NAME ,DEVICE_ID ,USER_ID , TRANS_TYPE, TRANS_DATE, IS_MOVED) "
                + " VALUES('" + deviceName + "','" + DeviceID + "','" + record.sdwEnrollNumber + "','"
                + record.idwInOutMode + "','" + record.time + "','n');");

            return serviceResult;
        }

        public static bool moveToRemoteTable_AlSorayai(string dbConnection, string dbType)
        {
            ServiceResult<DataTable> newTransactions = getNewTransactions();
            foreach (DataRow transaction in newTransactions.Result.Rows)
            {
                if (terminated)
                    return false;
                try
                {
                    if(IsRunning)
                        AttendenceService.updateAttendence(transaction, dbConnection, dbType);
                }
                catch (Exception e)
                {
                    SystemContext.Log(SystemContext.RootLogger, LogLevel.ERROR, e.Message);
                }
            }
            return true;
        }

        public static bool moveToRemoteTable(String Customer)
        {
            ServiceResult<DataTable> result = MappingService.getMappingInfo();

            if (result == null || !result.IsSuccess() || result.Result.Rows.Count == 0)
            {
                SystemContext.Log(SystemContext.RootLogger, LogLevel.CORE, "Could not get mapping info");
                return false;
            }
            if (!result.IsSuccess())
            {
                SystemContext.Log(SystemContext.RootLogger, LogLevel.CORE, result.ErrorMessage.englishValue);
            }

            DataRow Mapping = result.Result.Rows[0];

            if (Customer.Equals("AL-Sorayai"))
            {
                return moveToRemoteTable_AlSorayai(Mapping["CONNECTION_STRING"].ToString().Trim(), Mapping["DB_TYPE"].ToString().Trim());
            }

            ServiceResult<DataTable> newTransactions = getNewTransactions();
            foreach (DataRow transaction in newTransactions.Result.Rows)
            {
                DateTime transactionTime = DateTime.Parse(transaction["TRANS_DATE"].ToString());
                String TransactionType = transaction["TRANS_TYPE"].ToString().Equals("0") ?
                    Mapping["IN_TRANS_MAPPING"].ToString() : Mapping["OUT_TRANS_MAPPING"].ToString();
                String deviceName = transaction["DEVICE_NAME"].ToString();

                String insertCommand = "INSERT INTO " + Mapping["TRANS_TABLE"].ToString().Trim() + "("
                    + Mapping["USER_ID_COL"].ToString().Trim() + "," + Mapping["TIME_COL"].ToString().Trim()
                    + "," + Mapping["TRANS_TYPE_COL"].ToString().Trim()
                    + "," + Mapping["DEVICE_NAME_COL"].ToString().Trim()
                    + ") VALUES('" + transaction["USER_ID"].ToString().Trim() + "','"
                    + transactionTime.ToString(Mapping["TIME_FORMAT"].ToString().Trim()) + "','"
                    + TransactionType.ToString().Trim() + "','" + deviceName + "')";

                ServiceResult<String> remoteResult = RemoteDAO.ExecuteNoneQuery(insertCommand,
                    Mapping["CONNECTION_STRING"].ToString().Trim(), Mapping["DB_TYPE"].ToString().Trim());

                String setMoveFlagQuery = "UPDATE TRANSACTIONS SET IS_MOVED = 'y'"
                        + "WHERE DEVICE_NAME = '" + transaction["DEVICE_NAME"].ToString() + "'"
                        + "AND USER_ID = '" + transaction["USER_ID"].ToString() + "'"
                        + "AND TRANS_TYPE = '" + transaction["TRANS_TYPE"].ToString() + "'"
                        + "AND TRANS_DATE = '" + transaction["TRANS_DATE"].ToString() + "';";

                if (remoteResult.IsSuccess())
                {
                    DAL.DAO.ExecuteNoneQuery(setMoveFlagQuery);
                }
                else
                {
                    if (!remoteResult.ErrorMessage.errorCode.Equals("-2146232060"))
                    {
                        string[] paramlist = new string[4];
                        paramlist[0] = transaction["DEVICE_NAME"].ToString();
                        paramlist[1] = transaction["USER_ID"].ToString();
                        paramlist[2] = transaction["TRANS_DATE"].ToString();
                        paramlist[3] = transaction["TRANS_TYPE"].ToString();

                        SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, remoteResult.ErrorMessage.englishValue, paramlist);
                    }
                    else
                        DAL.DAO.ExecuteNoneQuery(setMoveFlagQuery);
                }
            }

            return true;
        }

        public static ServiceResult<DataTable> testRemoteTable(Mapping mapping)
        {
            String query = "Select " + mapping.UserIDColumnName + ","
                + mapping.TimeColumnName + "," + mapping.TransactionTypeColumnName
                + " from " + mapping.TableName;

            ServiceResult<DataTable> result =
                RemoteDAO.ExecuteQuery(query, mapping.ConnectionString, mapping.ConnectionProvider);
            return result;
        }

        public static ServiceResult<DataTable> getNewTransactions()
        {
            return DAO.ExecuteQuery("SELECT * FROM TRANSACTIONS WHERE IS_MOVED = 'n' order by TRANS_DATE");
        }

        public static ServiceResult<DataTable> getDeviceTransactions(int deviceID)
        {
            return DAO.ExecuteQuery("SELECT * FROM TRANSACTIONS WHERE DEVICE_ID = " + deviceID + " order by TRANS_DATE");
        }

        public static Dictionary<string, Dictionary<long, string>> getDeviceTransactionsDict(int deviceID)
        {
            ServiceResult<DataTable> result = 
                DAO.ExecuteQuery("SELECT * FROM TRANSACTIONS WHERE DEVICE_ID = " + deviceID + " order by TRANS_DATE");
            Dictionary<string, Dictionary<long, string>> dict= new Dictionary<string, Dictionary<long, string>>{};
            if (result.Result == null)
                return dict;
            foreach (DataRow transaction in result.Result.Rows)
            {
                DateTime transactionTime = DateTime.Parse(transaction["TRANS_DATE"].ToString());
                String userID = transaction["USER_ID"].ToString().Trim();
                if(dict.ContainsKey(userID)){
                    dict[userID].Add(transactionTime.Ticks, "");
                }
                else
                {
                    dict.Add(userID, new Dictionary<long,string>{});
                    dict[userID].Add(transactionTime.Ticks, "");
                }
            }
            return dict;
        }

        public static void StartMoveService(string strCustomer)
        {
            while (true)
            {
                if(IsRunning)
                    moveToRemoteTable(strCustomer);
                if (terminated)
                    break;
                Thread.Sleep(10000);
            }
        }
    }

    public class LogRecord
    {
        public string sdwEnrollNumber = "";
        public int idwTMachineNumber = 0;
        public int idwEMachineNumber = 0;
        public int idwVerifyMode = 0;
        public int idwInOutMode = 0;
        public DateTime time;
        public int idwWorkcode = 0;

        public LogRecord(int iMachineNumber, String sdwEnrollNumber, int idwVerifyMode, int idwInOutMode, int idwYear,
            int idwMonth, int idwDay, int idwHour, int idwMinute, int idwSecond, int idwWorkcode)
        {
            this.sdwEnrollNumber = sdwEnrollNumber;//modify by Darcy on Nov.26 2009
            this.idwVerifyMode = idwVerifyMode;
            this.idwInOutMode = idwInOutMode;
            this.time = DateTime.Parse(idwYear.ToString() + "-" + idwMonth.ToString() + "-" + idwDay.ToString() +
                " " + idwHour.ToString() + ":" + idwMinute.ToString() + ":" + idwSecond.ToString());
            this.idwWorkcode = idwWorkcode;
        }
    }
}
