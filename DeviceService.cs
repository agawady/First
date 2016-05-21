using Helper.Domains;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Services
{
    public class DeviceService
    {
        public static ServiceResult<DataTable> getActiveDevices()
        {
            return DAL.DAO.ExecuteQuery("SELECT * FROM DEVICES WHERE ACTIVE = 'y' AND "
                + " ID > (SELECT PARAM_VALUE FROM SYS_PARAMS WHERE PARAM_NAME = 'CurrentDevice') "
                + " ORDER BY ID");
        }

        public static ServiceResult<DataTable> getDisconnectedDevices()
        {
            return DAL.DAO.ExecuteQuery("SELECT * FROM DEVICES D WHERE ACTIVE = 'y' AND CONNECT_STATUS = 'n' AND  "
                + " (SELECT COUNT(*) FROM TRANSACTIONS T WHERE SYSDATETIME() - TRANS_DATE < 1 AND D.DEVICE_IP = T.DEVICE_NAME) = 0 "
                + " ORDER BY ID");
        }

        public static ServiceResult<DataTable> getPeriorityDevice()
        {
            return DAL.DAO.ExecuteQuery("SELECT * FROM DEVICES WHERE "
                + " ID = (SELECT PARAM_VALUE FROM SYS_PARAMS WHERE PARAM_NAME = 'PeriorityDevice') ");
        }

        public static void deleteDevice(int ID)
        {
            String insertCommand = "DELETE FROM DEVICES WHERE ID = " + ID + ";";
            DAL.DAO.ExecuteNoneQuery(insertCommand);
        }

        public static void addDevice(String deviceName, String deviceIP, String devicePort, String deviceType)
        {
            String insertCommand = "INSERT INTO DEVICES(DEVICE_NAME, DEVICE_IP, DEVICE_PORT, DEVICE_TYPE)" +
                "VALUES ('" + deviceName + "','" + deviceIP + "','" + devicePort + "','" + deviceType + "')";
            DAL.DAO.ExecuteNoneQuery(insertCommand);
        }

        public static void updateDevice(String deviceName, String deviceIP, String devicePort, String deviceType, int ID)
        {
            String updateCommand = "UPDATE DEVICES SET DEVICE_NAME = '" + deviceName + "',DEVICE_IP = '"
                + deviceIP + "', DEVICE_PORT = '" + devicePort + "', DEVICE_TYPE = '" + deviceType
                + "' WHERE ID = '" + ID + "';";
            DAL.DAO.ExecuteNoneQuery(updateCommand);
        }

        public static void updateDeviceConnectStatus(bool connected, int ID)
        {
            String status = connected ? "y" : "n";
            String updateCommand = "UPDATE DEVICES SET CONNECT_STATUS = '" + status
                + "' WHERE ID = '" + ID + "';";
            DAL.DAO.ExecuteNoneQuery(updateCommand);
        }

        public static void updateDeviceConnectStatus(bool connected, String deviceIP)
        {
            String status = connected ? "y" : "n";
            String updateCommand = "UPDATE DEVICES SET CONNECT_STATUS = '" + status
                + "' WHERE DEVICE_IP = '" + deviceIP + "';";
            DAL.DAO.ExecuteNoneQuery(updateCommand);
        }

        public static void updateDeviceTransactionCount(String deviceIP, int count)
        {
            try
            {
                String updateCommand = "UPDATE DEVICES SET COUNT = " + count
                    + " WHERE DEVICE_IP = '" + deviceIP + "';";
                DAL.DAO.ExecuteNoneQuery(updateCommand);
            }
            catch { }
        }
    }
}
