using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Future2.Classes
{
    public class ProjectUtil
    {
        /// <summary>
        /// Exception To String
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string ErrToStr(Exception ex)
        {
            StringBuilder buff = new StringBuilder();
            buff.Append(string.Concat(new object[] { "Exception.Type : ", ex.GetType().Name, "\r\nException.Message : ", ex.Message, "\r\nException.TargetSite: ", ex.TargetSite, "\r\nException.StackTrace: \r\n", ex.StackTrace }));
            return buff.ToString();
        }
    }
}
