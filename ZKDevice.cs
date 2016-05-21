using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessLayer.Services;
using BusinessLayer.Context;
using Helper.Domains;
using System.Data;
using System.Threading;
using System.ComponentModel;

namespace BusinessLayer.Services
{
    public class ZKDevice
    {
        //Create Standalone SDK class dynamicly.
        public zkemkeeper.CZKEMClass axCZKEM1 = new zkemkeeper.CZKEMClass();

        private bool bIsConnected = false;//the boolean value identifies whether the device is connected
        private int iMachineNumber = 167;//the serial number of the device.After connecting the device ,this value will be changed.

        public String DeviceIP;
        public String DeviceID;
        public String DevicePort;
        public String DeviceName;

        public static String Customer;
        public static bool IsRunning = false;
        public static BackgroundWorker worker = null;

        public delegate int AsyncMethodCaller();
        private static int connectTimeout; //sec
        List<LogRecord> transactionLog = new List<LogRecord> { };
        public String DeviceFullName
        {
            get { return DeviceName + "(" + DeviceIP + ")"; }
        }

        public static void ReadAll(string strCustomer, BackgroundWorker _bw)
        {
            worker = _bw;
            Customer = strCustomer;

            try
            {
                connectTimeout = int.Parse(ParamService.getParameterValue("ConnectTimeout"));
            }
            catch
            {
                connectTimeout = 200;
            }

            while (true)
            {
                //break;
                ReadAll();
                Thread.Sleep(3000);
            }
        }
        private static void ReadAll()
        {
            if (IsRunning)
            {
                ServiceResult<DataTable> devices = null;
                bool isPeriorityDevice = !String.IsNullOrEmpty(ParamService.getParameterValue("PeriorityDevice"));
                try
                {
                    if (isPeriorityDevice)
                    {
                        devices = DeviceService.getPeriorityDevice();
                        ParamService.updateParameter("PeriorityDevice", null);
                    }
                    else
                        devices = DeviceService.getActiveDevices();
                }
                catch (Exception e)
                {
                    string message = "Error getting devices list: " + e.Message;
                    SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.ERROR, message, null, true);
                    worker.ReportProgress(0, new ProgressRecord(DateTime.Now, false, message));
                    return;
                }
                int totalCount = 0;
                if (devices.IsSuccess())
                {
                    foreach (DataRow row in devices.Result.Rows)
                    {
                        //Thread.Sleep(5000);
                        if (!IsRunning)
                            return;
                        int count = 0;
                        ZKDevice device = new ZKDevice();
                        if (!String.IsNullOrEmpty(ParamService.getParameterValue("PeriorityDevice")))
                            break;
                        try
                        {                        
                            device.DeviceID = row["ID"].ToString();
                            device.DeviceName = row["DEVICE_NAME"].ToString();
                            device.DeviceIP = row["DEVICE_IP"].ToString();
                            device.DevicePort = row["DEVICE_PORT"].ToString();
                            worker.ReportProgress(0, new ProgressRecord(DateTime.Now, true,
                                "Pooling Data From:  " + device.DeviceFullName));
                            if (!isPeriorityDevice)
                            {
                                if (device.DeviceID != devices.Result.Rows[devices.Result.Rows.Count - 1]["ID"].ToString())
                                    ParamService.updateParameter("CurrentDevice", device.DeviceID);
                                else
                                    ParamService.updateParameter("CurrentDevice", "0");
                            }
                            count = device.GetGeneralLogData();
                            totalCount += count;                            
                        }
                        catch (Exception e)
                        {
                            string message = "Error reading from device  " + device.DeviceFullName + "  " + e.Message;
                            SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.ERROR, message, null, true);
                            worker.ReportProgress(0, new ProgressRecord(DateTime.Now, false, message));
                        }
                    }
                }
                /*worker.ReportProgress(0, new ProgressRecord(DateTime.Now, true,
                                "Coping Log Data ..."));
                TransactionService.moveToRemoteTable(Customer);*/
            }
        }
        

        public static int TestConnection(String IP, String port)
        {
            bool isConnected = false;
            zkemkeeper.CZKEMClass axCZKEM1 = new zkemkeeper.CZKEMClass();
            int idwErrorCode = 0;
            try
            {               
                isConnected = axCZKEM1.Connect_Net(IP.Trim(), Convert.ToInt32(port.Trim()));
                if (!isConnected)
                    axCZKEM1.GetLastError(ref idwErrorCode);
            }
            catch (Exception ex) { axCZKEM1.GetLastError(ref idwErrorCode); }

            if (!isConnected && idwErrorCode == 0)
                return -1;

            return idwErrorCode;
        }

        //If your device supports the TCP/IP communications, you can refer to this.
        //when you are using the tcp/ip communication,you can distinguish different devices by their IP address.
        private int connect()
        {
            if (String.IsNullOrEmpty(DeviceIP) || String.IsNullOrEmpty(DevicePort))
                return -1;
            int idwErrorCode = 0;

            bIsConnected = axCZKEM1.Connect_Net(DeviceIP.Trim(), Convert.ToInt32(DevicePort.Trim()));
            if (bIsConnected == true)
            {
                iMachineNumber = 1;//In fact,when you are using the tcp/ip communication,this parameter will be ignored,that is any integer will all right.Here we use 1.
                axCZKEM1.RegEvent(iMachineNumber, 65535);//Here you can register the realtime events that you want to be triggered(the parameters 65535 means registering all)
                DeviceService.updateDeviceConnectStatus(true, DeviceIP);
            }
            else
            {
                axCZKEM1.GetLastError(ref idwErrorCode);
                // log here
            }
            return idwErrorCode;
        }

        //Download the attendance records from the device(For both Black&White and TFT screen devices).
        public int downloadGeneralLogData()
        {
            int count = 0;
            int connectResult = 0;
            if (!bIsConnected)
            {
                connectResult = connect();
            }
            if (!bIsConnected)
            {
                string[] paramlist = new string[4];
                paramlist[0] = DeviceName;
                paramlist[1] = "";
                paramlist[2] = "";
                paramlist[3] = "";

                if (connectResult == 0)
                    axCZKEM1.GetLastError(ref connectResult);

                String message = "Connection to device " + DeviceFullName + " failed, errorCode:" + connectResult.ToString();
                SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, message, paramlist, true);
                worker.ReportProgress(0, new ProgressRecord(DateTime.Now, false, message));

                DeviceService.updateDeviceConnectStatus(false, DeviceIP);

                return 0;
            }
                

            string sdwEnrollNumber = "";
            int idwVerifyMode=0;
            int idwInOutMode=0;
            int idwYear=0;
            int idwMonth=0;
            int idwDay=0;
            int idwHour=0;
            int idwMinute=0;
            int idwSecond = 0;
            int idwWorkcode = 0;
           
            int idwErrorCode=0;

            DeviceService.updateDeviceConnectStatus(true, DeviceIP);

            

            axCZKEM1.EnableDevice(iMachineNumber, false);//disable the device
            if (axCZKEM1.ReadGeneralLogData(iMachineNumber))//read all the attendance records to the memory
            {
                while (axCZKEM1.SSR_GetGeneralLogData(iMachineNumber, out sdwEnrollNumber, out idwVerifyMode,
                           out idwInOutMode, out idwYear, out idwMonth, out idwDay, out idwHour, out idwMinute, out idwSecond, ref idwWorkcode))//get records from the memory
                {
                    LogRecord record = new LogRecord(iMachineNumber, sdwEnrollNumber, idwVerifyMode,
                           idwInOutMode, idwYear, idwMonth, idwDay, idwHour, idwMinute, idwSecond, idwWorkcode);
                    transactionLog.Add(record);
                }               
            }
            else
            {
                axCZKEM1.GetLastError(ref idwErrorCode);
                string[] paramlist = new string[4];
                paramlist[0] = DeviceName;
                paramlist[1] = "";
                paramlist[2] = "";
                paramlist[3] = "";
                String message = "Connection to device " + DeviceFullName + " failed, errorCode:" + idwErrorCode.ToString();
                SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, message, paramlist, true);
                worker.ReportProgress(0, new ProgressRecord(DateTime.Now, false, message));
            }
            axCZKEM1.EnableDevice(iMachineNumber, true);//enable the device
            return count;
        }

        public int GetGeneralLogData()
        {
            int count = 0;
            DateTime startTime = DateTime.Now;
            Worker workerObject = new Worker();
            workerObject.device = this;
            Thread workerThread = new Thread(workerObject.DoWork);
            workerThread.IsBackground = true;
            workerThread.Start();
            Console.WriteLine("main thread: Starting worker thread...");

            while (!workerThread.IsAlive) ;

            while (workerThread.IsAlive)
            {
                if (DateTime.Now - startTime > new TimeSpan(0, 0, connectTimeout))
                {
                    string[] paramlist = new string[4];
                    paramlist[0] = DeviceName; paramlist[1] = ""; paramlist[2] = ""; paramlist[3] = "";
                    String message = "Connection to device " + DeviceFullName + " failed, Timeout";
                    SystemContext.LogWithParams(SystemContext.RootLogger, LogLevel.CORE, message, paramlist, true);
                    worker.ReportProgress(0, new ProgressRecord(DateTime.Now, false, message));
                    DeviceService.updateDeviceConnectStatus(false, DeviceIP);             
                    axCZKEM1.CancelOperation();
                    workerThread.Abort();
                    worker.ReportProgress(0, new ProgressRecord(999999));
                    IsRunning = false;
                    Thread.Sleep(Int32.MaxValue); // do nothing because bg worker will be killed by owner thread
                }                
            }
            DeviceService.updateDeviceTransactionCount(DeviceIP, transactionLog.Count);             
            if (transactionLog.Count > 0)
            {
                // Move data to our table
                count = TransactionService.insertLog(transactionLog, DeviceID, DeviceIP);
                String message = count + " Records were read from device " + DeviceFullName;
                worker.ReportProgress(0, new ProgressRecord(DateTime.Now, false, message));
            }
            return count;
        }

        //Clear all attendance records from terminal (For both Black&White and TFT screen devices).
        public void ClearGLog()
        {
            if (bIsConnected == false)
            {
                connect();
            }
            int idwErrorCode = 0;

            axCZKEM1.EnableDevice(iMachineNumber, false);//disable the device
            if (axCZKEM1.ClearGLog(iMachineNumber))
            {
                axCZKEM1.RefreshData(iMachineNumber);//the data in the device should be refreshed
                // log here
            }
            else
            {
                axCZKEM1.GetLastError(ref idwErrorCode);
                // log here
            }
            axCZKEM1.EnableDevice(iMachineNumber, true);//enable the device
        }

        //Get the count of attendance records in from ternimal(For both Black&White and TFT screen devices).
        private int GetDeviceLogCount(object sender, EventArgs e)
        {
            if (bIsConnected == false)
                connect();

            int idwErrorCode = 0;
            int iValue = 0;

            axCZKEM1.EnableDevice(iMachineNumber, false);//disable the device
            if (axCZKEM1.GetDeviceStatus(iMachineNumber, 6, ref iValue)) //Here we use the function "GetDeviceStatus" to get the record's count.The parameter "Status" is 6.
            {
                return iValue;
            }
            else
            {
                axCZKEM1.GetLastError(ref idwErrorCode);
                // log
            }
            axCZKEM1.EnableDevice(iMachineNumber, true);//enable the device
            return -1;
        }

        public class ProgressRecord
        {
            private DateTime time;
            public DateTime Time
            {
                get { return time; }
                set { time = value; }
            }

            private int error;
            public int Error
            {
                get { return error; }
                set { error = value; }
            }

            private bool isInfo;
            public bool IsInfo
            {
                get { return isInfo; }
                set { isInfo = value; }
            }
            private String message;
            public String Message
            {
                get { return message; }
                set { message = value; }
            }

            public ProgressRecord(DateTime time, bool isInfo, string message)
            {
                this.time = time;
                this.isInfo = isInfo;
                this.message = message;
                this.error = 0;
            }
            public ProgressRecord(int error)
            {
                this.error = error;
            }
        }
        public class Worker
        {
            public ZKDevice device;
            public void DoWork()
            {
                try
                {
                    device.downloadGeneralLogData();
                }
                catch { }
            }
        }
    }
}
