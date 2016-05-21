using BusinessLayer.Context;
using DAL;
using Helper.Domains;
using Helper.Handlers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;

namespace BusinessLayer.Services
{
    public class SecurityService
    {
        #region security implementation
        public static string UserID(Page page)
        {
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];
            if (userContext == null)
                return "";
            else
                return (userContext.session.userID.ToString());
        }

        public static string UserLoginName(Page page)
        {
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];
            if (userContext == null)
                return "";
            else
                return userContext.session.userLoginName;
        }

        public static string UserName(Page page)
        {
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];
            if (userContext == null)
                return "";
            else
            {
                if (userContext.session.userLanguage == Language.AR)
                {
                    return userContext.session.userNameAr;
                }
                else if (userContext.session.userLanguage == Language.EN)
                {
                    return userContext.session.userNameEn;
                }
                else
                {
                    return userContext.session.userNameEn;
                }
            }
        }

        public static string UserLanguage(Page page)
        {
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];
            if (userContext == null)
                return "";
            else
                return userContext.session.userLanguage.ToString();
        }

        public static void changeUserLanguage(Page page, Language lang)
        {
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];
            if (userContext == null)
                return;
            else
            {
                userContext.session.userLanguage = lang;
                page.Session[UserContext.userSessionID] = userContext;
            }
        }

        public static DateTime LoginTime(Page page)
        {
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];
            if (userContext == null)
                return DateTime.Now;
            else
                return userContext.session.loginTime;
        }

        public static ServiceResult<bool> changePassword(Page page, string oldPassword, string newPassword)
        {
            ServiceResult<bool> returnData = new ServiceResult<bool>();
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];
            if (userContext == null)
            {
                // لا يمكن تغير كلمة المرور - يجب تسجيل الدخول اولا
                returnData.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.CHANGE_PASSWORD_USER_SESSION_TERMINATED));

                // log action
                SystemContext.Log(SystemContext.RootLogger, LogLevel.ERROR, "failed to update password > user name: " + userContext.session.userLoginName + " > " + SystemContext.getErrorMsg(ErrorHandler.CHANGE_PASSWORD_USER_SESSION_TERMINATED));

                return returnData;
            }

            ServiceResult<DataTable> userResult = getUserInfo(userContext.session.userLoginName);
            if (userResult.IsSuccess())
            {
                if (!MatchHash(((string)userResult.Result.Rows[0]["PASSWORD"]).Replace("\\0", "\0"), oldPassword))
                {
                    // لا يمكن تغير كلمة المرور - كلمة المرور لا تنطبق
                    returnData.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.CHANGE_PASSWORD_OLD_PASSWORD_NOT_CORRECT));

                    // log action
                    SystemContext.Log(page, SystemContext.RootLogger, LogLevel.ERROR, "failed to update password > user name: " + userContext.session.userLoginName + " > " + SystemContext.getErrorMsg(ErrorHandler.CHANGE_PASSWORD_OLD_PASSWORD_NOT_CORRECT));
                }
                else
                {
                    string query = "update SEC_USER set PASSWORD= '" + CreateHash(newPassword).Replace("'", "''") + "' where USER_LOGIN_NAME = '" + userContext.session.userLoginName + "'";


                    ServiceResult<int> updateData = DAO.ExecuteNoneQuery(query);
                    if (updateData.IsSuccess())
                    {
                        returnData.serviceSuccessed(true);

                        // log action
                        SystemContext.Log(page, SystemContext.RootLogger, LogLevel.INFO, "change password done successfuly > user name: " + userContext.session.userLoginName);

                    }
                    else
                    {
                        returnData.serviceFailed(updateData.ErrorMessage);

                        // log action
                        SystemContext.Log(page, SystemContext.RootLogger, LogLevel.ERROR, "failed to update password in DB > user name: " + userContext.session.userLoginName);

                    }
                }
            }
            else
            {
                // user not exist
                returnData.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.INVALID_USER_NAME_OR_PASSWORD));
                // log action
                SystemContext.Log(SystemContext.RootLogger, LogLevel.ERROR, "invalid try to change password > user name: " + userContext.session.userLoginName + " > " + SystemContext.getErrorMsg(ErrorHandler.INVALID_USER_NAME_OR_PASSWORD));
            }
            return returnData;
        }

        public static ServiceResult<bool> login(Page page, string userLoginName, string password)
        {
            ServiceResult<bool> loginResult = new ServiceResult<bool>();

            ServiceResult<DataTable> userResult = getUserInfo(userLoginName);
            if (userResult.IsSuccess() && userResult.Result != null && userResult.Result.Rows.Count != 0)
            {
                var encryptedUserPassword = ((string)userResult.Result.Rows[0]["PASSWORD"]).Replace("\\0", "\0");
                /*DateTime userPasswordExpiration;

                //check password expiration date
                var passExp = userResult.Result.Rows[0]["PASSWORD_EXP_DATE"];
                if (passExp != null
                    && !string.IsNullOrEmpty(passExp.ToString())
                    && DateTime.TryParse(passExp.ToString(), out userPasswordExpiration))
                {
                    if (DateTime.Now >= userPasswordExpiration)
                    {
                        loginResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.PASSWORD_EXPIRED));

                        // log action
                        SystemContext.Log(SystemContext.RootLogger, LogLevel.ERROR, "login failed > password Expired > user name: " + userLoginName);

                        return loginResult;
                    }
                }*/

                // check password match
                if (!MatchHash(encryptedUserPassword, password))
                {
                    loginResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.INVALID_USER_NAME_OR_PASSWORD));

                    // log action
                    SystemContext.Log(SystemContext.RootLogger, LogLevel.ERROR, "login failed > password does not matched > user name: " + userLoginName);

                    return loginResult;
                }
                else
                {
                    // create and add user context to HTTP session
                    UserContext userContext = new UserContext();
                    int userId = int.Parse(userResult.Result.Rows[0]["USER_ID"].ToString());
                    string userNameAr = userResult.Result.Rows[0]["USER_NAME_AR"].ToString();
                    string userNameEn = userResult.Result.Rows[0]["USER_NAME_EN"].ToString();
                    int userDeptID = 0;//int.Parse(userResult.Result.Rows[0]["DEPARTMENT_ID"].ToString());
                    string userEmail = userResult.Result.Rows[0]["EMAIL"].ToString();
                    string userMobile = userResult.Result.Rows[0]["MOBILE"].ToString();
                    string userTele = userResult.Result.Rows[0]["TELEPHONE"].ToString();
                    string userAddress = userResult.Result.Rows[0]["ADDRESS"].ToString();
                    Language userLang = (Language)Enum.Parse(typeof(Language), userResult.Result.Rows[0]["LANGUAGE"].ToString(), true);
                    userContext.createUserContext(page, userId, userLoginName, userNameAr, userNameEn, userDeptID, userEmail, userMobile, userTele, userAddress, userLang);

                    page.Session[UserContext.userSessionID] = userContext;

                    loginResult.serviceSuccessed(true);

                    // log action
                    SystemContext.Log(page, SystemContext.RootLogger, LogLevel.INFO, "Session created sucessfuly and login Done > userId: " + userId + ", userNameAr: " + userNameAr + ", userNameEn: " + userNameEn + ", userDeptID: " + userDeptID + ", userEmail: " + userEmail + ", userMobile: " + userMobile + ", userTele: " + userTele + ", userAddress: " + userAddress + ", userLoginName: " + userLoginName + ", language = " + userLang.ToString());

                    return loginResult;
                }
            }
            else
            {
                if (!userResult.IsSuccess())
                {
                    // cannot connect to DB to fetch user 
                    if (SystemContext.getErrorMsg(ErrorHandler.SERVER_OUT_OF_SERVICE) != null)
                    {
                        loginResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.SERVER_OUT_OF_SERVICE));
                    }
                    else
                    {
                        loginResult.serviceFailed(new ErrorMessage(ErrorHandler.SERVER_OUT_OF_SERVICE, "SecurityService.Login", ErrorType.DB, ErrorSeverity.FATAL, ErrorHandler.SERVER_OUT_OF_SERVICE, ErrorHandler.SERVER_OUT_OF_SERVICE));
                    }

                    // log action
                    SystemContext.Log(SystemContext.RootLogger, LogLevel.ERROR, "login failed > user name: " + userLoginName + " > " + ErrorHandler.SERVER_OUT_OF_SERVICE);
                }
                else
                {
                    // user not exist
                    loginResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.INVALID_USER_NAME_OR_PASSWORD));

                    // log action
                    SystemContext.Log(SystemContext.RootLogger, LogLevel.ERROR, "login failed > user name: " + userLoginName + " > User does not Exist");

                }

                return loginResult;
            }
        }

        public static ServiceResult<bool> authenticate(Page page, string userLoginName, string password)
        {
            ServiceResult<bool> authResult = new ServiceResult<bool>();

            ServiceResult<DataTable> userResult = getUserInfo(userLoginName);

            if (userResult.IsSuccess() && userResult.Result != null && userResult.Result.Rows.Count != 0)
            {
                var encryptedUserPassword = ((string)userResult.Result.Rows[0]["PASSWORD"]).Replace("\\0", "\0");

                if (!MatchHash(encryptedUserPassword, password))
                {
                    // password does not match
                    authResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.INVALID_USER_NAME_OR_PASSWORD));

                    // log action
                    SystemContext.Log(page, SystemContext.RootLogger, LogLevel.ERROR, "login failed > password does not matched > user name: " + userLoginName);

                }
                else
                {
                    // password match
                    authResult.serviceSuccessed(true);

                    // log action
                    SystemContext.Log(page, SystemContext.RootLogger, LogLevel.INFO, "authentication done > user name: " + userLoginName);
                }
            }
            else
            {
                // user not exist
                authResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.INVALID_USER_NAME_OR_PASSWORD));

                // log action
                SystemContext.Log(page, SystemContext.RootLogger, LogLevel.ERROR, "authentication failed > invalid username or password > user name: " + userLoginName);
            }

            return authResult;
        }

        public static bool IsPermittedPage(HttpContext context, string pageName, string pageMode, string rawURL)
        {
            UserContext userContext = (UserContext)context.Session[UserContext.userSessionID];
            if (userContext == null)
                return false;


            bool isPermittedPage = false;
            string PageNameASPX = getPageName(rawURL, true);
            string pageNameWithParams = getPageName(rawURL, false);

            // if page name aspx exist for permitted pages for this user so no need to check the params.
            isPermittedPage = userContext.session.permittedPages.Contains(PageNameASPX.ToLower().Trim()) || userContext.session.permittedPages.Contains(pageNameWithParams.ToLower().Trim());

            if (!isPermittedPage)
            {
                // log action
                SystemContext.Log(userContext, SystemContext.RootLogger, LogLevel.ERROR, "User > " + userContext.session.userLoginName + " > tried to  access unautherized page > Page name: " + PageNameASPX.ToLower().Trim());
            }
            return isPermittedPage;
        }

        private static string getPageName(string rawURL, bool onlyPageName)
        {

            int startPos = rawURL.LastIndexOf("/");
            startPos = (startPos < 0) ? 0 : startPos + 1;
            int lasPos = rawURL.Length;

            if (onlyPageName)
            {
                lasPos = rawURL.LastIndexOf(".aspx");
                lasPos = (lasPos < 0) ? rawURL.Length : lasPos + 5;
            }

            return rawURL.Substring(startPos, (lasPos - startPos));
        }
        public static bool hasExportPrivilege(Page page)
        {
            UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];

            if (userContext == null)
                return false;

            bool hasExportPrivilege = false;

            hasExportPrivilege = userContext.session.privilegeFunctions.Contains("PRV_EXPORT".ToLower());

            return hasExportPrivilege;
        }

        public static ServiceResult<bool> logout(Page page)
        {
            ServiceResult<bool> authResult = new ServiceResult<bool>();

            try
            {
                UserContext userContext = (UserContext)page.Session[UserContext.userSessionID];

                if (userContext != null)
                {
                    // user session destroyed
                    authResult.serviceSuccessed(true);

                    SystemContext.audit(page, AuditOperationType.LOGOUT, "Logout", "Logout Success For User: " + userContext.session.userLoginName + " on " + DateTime.Now.ToString("ddd d MMM yyyy - hh:mm:ss tt"));

                    // log action
                    SystemContext.Log(page, SystemContext.RootLogger, LogLevel.INFO, "logout done > user: " + userContext.session.userLoginName + " > logout Date:" + DateTime.Now.ToString("ddd d MMM yyyy - hh:mm:ss tt"));

                    page.Session[UserContext.userSessionID] = null;
                }
                else
                {
                    // cannot destroy user session
                    authResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.CANNOT_DESTROY_USER_SESSION));

                    // log action
                    SystemContext.Log(page, SystemContext.RootLogger, LogLevel.ERROR, "logout failed > cannot destroy user session > trying Date:" + DateTime.Now.ToString("ddd d MMM yyyy - hh:mm:ss tt"));
                }

                // clear user httpsession
                page.Session.Clear();
            }
            catch (Exception)
            {
                authResult.serviceFailed(SystemContext.getErrorMsg(ErrorHandler.CANNOT_DESTROY_USER_SESSION));

                // log action
                SystemContext.Log(page, SystemContext.RootLogger, LogLevel.ERROR, "logout failed > cannot destroy user session > trying Date:" + DateTime.Now.ToString("ddd d MMM yyyy - hh:mm:ss tt"));
            }

            return authResult;
        }
        #endregion

        #region help methods
        public static string CreateHash(string unHashed)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider x = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] data = System.Text.Encoding.ASCII.GetBytes(unHashed);
            data = x.ComputeHash(data);
            return System.Text.Encoding.ASCII.GetString(data);
        }

        internal static bool MatchHash(string HashData, string HashUser)
        {
            HashUser = CreateHash(HashUser);
            if (HashUser == HashData)
                return true;
            else
                return false;
        }

        internal static ServiceResult<DataTable> getUserInfo(string userloginName)
        {
            ServiceResult<DataTable> serviceResult = new ServiceResult<DataTable>();

            // only select active users
            string query = "select * FROM SEC_USER where UPPER(USER_LOGIN_NAME) = UPPER('" + userloginName + "') and STATUS = 'A'";

            serviceResult = DAO.ExecuteQuery(query);
            return serviceResult;
        }

        #endregion
    }
}
