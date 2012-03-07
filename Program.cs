namespace QvxEventLogConnector
{
    #region Usings
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using QvxLib;
    using System.IO;
    using System.Threading;
    #endregion

    class Program
    {
        static void Main(string[] args)
        {
            #region EventLogEntry -> QVX Serializer
            // it is not possible to add attributes to this already compiled and existing class
            // you have to give an collection of attributes to the serializer to override
            // the default behavior
            var overideAttributes = new Dictionary<string, List<QvxBaseAttribute>>() {
                    {"Data", new List<QvxBaseAttribute>() { new QvxIgnoreAttribute(true) }},
                    {"ReplacementStrings", new List<QvxBaseAttribute>() { new QvxSubfieldAttribute("|") }},
            };

            var qvxSer = new QvxSerializer<System.Diagnostics.EventLogEntry>(overideAttributes);       
            #endregion    
         
            #region Request Handler
            var handler = new QvxDefaultHandleRequestHandler();         

            handler.QvxEditConnectHandler = (c, d) =>
               {
                   // we don't need a edit Dialog
                   return new QvxReply() { OutputValues = { "" }, Result = QvxResult.QVX_OK };
               };

            handler.QvxConnectHandler = (c) =>
            {
                return new QvxReply() { Result = QvxResult.QVX_OK };
            };

            handler.QvxDisconnectHandler = () =>
            {
                return new QvxReply() { Result = QvxResult.QVX_OK };
            }; 
            #endregion

            #region Execute Handler
            var excHand = new QvxQvxExecuteCommandHandler();

            excHand.QvxExecuteRequestTablesHandler = () =>
            {
                return
                    from c in System.Diagnostics.EventLog.GetEventLogs()
                    select new QvxTablesRow(c.Log);
            };
          
            excHand.QvxExecuteRequestColumnsHandler = (table) =>
            {
                return
                    from c in qvxSer.QvxTableHeader.Fields
                    select new QvxColumsRow(table, c.FieldName);
            };

            excHand.QvxExecuteRequestSelectHandler = (sql, c) =>
            {
                var qvxresult = QvxResult.QVX_SYNTAX_ERROR;

                Action<QvxDataClient> tmpAction = null;
                 
                string eventlogName="";
                try
                {
                    // extract the eventlog name from the SQL
                    eventlogName = sql.Substring(sql.ToUpper().IndexOf("FROM") + 4).Trim();                  
                }
                catch
                {
                }
                var enventlog = (from evlog in System.Diagnostics.EventLog.GetEventLogs() where evlog.Log == eventlogName select evlog).FirstOrDefault();

                if (enventlog != null)
                {
                    tmpAction = (dataclient) =>
                    {
                        qvxSer.Serialize(enventlog.Entries.Cast<System.Diagnostics.EventLogEntry>(), new BinaryWriter(dataclient));

                        dataclient.Close();
                    };

                    qvxresult = QvxResult.QVX_OK;
                }
             
                return new Tuple<QvxResult, Action<QvxDataClient>>(qvxresult, tmpAction);
            }; 
            #endregion

            #region Generic Command Handler
            var generic = new QvxDefaultQvxGenericCommandHandler();
            generic.HaveStarField = true; 
            #endregion

            #region Wireup
            // Connect the execute handler to the default handler
            handler.QvxExecuteHandler = excHand.HandleRequest;

            handler.QvxGenericCommandHandler = generic.HandleRequest;

            var client = new QvxCommandClient(args);

            // Connect the default handler to the qvx pipe client
            client.HandleQvxRequest = handler.HandleRequest;

            #endregion

            // Start the Client
            client.StartThread();

            // Wait or do other stuff :-)
            while (client.Running) Thread.Sleep(500);             
        }
    }
}