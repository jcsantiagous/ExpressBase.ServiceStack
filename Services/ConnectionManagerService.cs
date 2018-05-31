﻿using ExpressBase.Common;
using ExpressBase.Common.Connections;
using ExpressBase.Common.Constants;
using ExpressBase.Common.Data;
using ExpressBase.Common.Data.MongoDB;
using ExpressBase.Common.ServiceClients;
using ExpressBase.Objects.ServiceStack_Artifacts;
using ServiceStack;
using ServiceStack.Messaging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace ExpressBase.ServiceStack.Services
{
    public class ConnectionManager : EbBaseService
    {
        public ConnectionManager(IEbConnectionFactory _dbf, IMessageProducer _mqp, IMessageQueueClient _mqc, IEbMqClient _mq) : base(_dbf, _mqp, _mqc, _mq)
        {
        }

        [Authenticate]
        public RefreshSolutionConnectionsAsyncResponse Post(RefreshSolutionConnectionsBySolutionIdAsyncRequest request)
        {
            RefreshSolutionConnectionsAsyncResponse res = new RefreshSolutionConnectionsAsyncResponse();
            try
            {
                res = this.MQClient.Post<RefreshSolutionConnectionsAsyncResponse>(new RefreshSolutionConnectionsBySolutionIdAsyncRequest() { TenantAccountId = request.TenantAccountId, UserId = request.UserId });
                return res;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception:" + e.ToString());
                res.ResponseStatus.Message = e.Message;
                return res;
            }
        }

        [Authenticate]
        public GetConnectionsResponse Post(GetConnectionsRequest req)
        {
            GetConnectionsResponse resp = new GetConnectionsResponse();
            resp.EBSolutionConnections = this.Redis.Get<EbConnectionsConfig>(string.Format(CoreConstants.SOLUTION_CONNECTION_REDIS_KEY, req.TenantAccountId));
            if (resp.EBSolutionConnections == null)
                using (var con = this.InfraConnectionFactory.DataDB.GetNewConnection() as Npgsql.NpgsqlConnection)
                {
                    con.Open();
                    string sql = @"SELECT con_type, con_obj FROM eb_connections WHERE solution_id = @solution_id AND eb_del = 'F'";
                    DataTable dt = new DataTable();
                    EbConnectionsConfig cons = new EbConnectionsConfig();

                    var ada = new Npgsql.NpgsqlDataAdapter(sql, con);
                    ada.SelectCommand.Parameters.Add(new Npgsql.NpgsqlParameter("solution_id", NpgsqlTypes.NpgsqlDbType.Text) { Value = req.TenantAccountId });
                    ada.Fill(dt);

                    if (dt.Rows.Count != 0)
                    {
                        foreach (DataRow dr in dt.Rows)
                        {
                            if (dr["con_type"].ToString() == EbConnectionTypes.EbDATA.ToString())
                                cons.DataDbConnection = EbSerializers.Json_Deserialize<EbDataDbConnection>(dr["con_obj"].ToString());
                            else if (dr["con_type"].ToString() == EbConnectionTypes.EbDATA_RO.ToString())
                                cons.DataDbConnection = EbSerializers.Json_Deserialize<EbDataDbConnection>(dr["con_obj"].ToString());
                            else if (dr["con_type"].ToString() == EbConnectionTypes.EbOBJECTS.ToString())
                                cons.ObjectsDbConnection = EbSerializers.Json_Deserialize<EbObjectsDbConnection>(dr["con_obj"].ToString());
                            //else if (dr["con_type"].ToString() == EbConnectionTypes.EbFILES.ToString())
                            //    cons.FilesDbConnection = EbSerializers.Json_Deserialize<EbFilesDbConnection>(dr["con_obj"].ToString());
                            else if (dr["con_type"].ToString() == EbConnectionTypes.EbLOGS.ToString())
                                cons.LogsDbConnection = EbSerializers.Json_Deserialize<EbLogsDbConnection>(dr["con_obj"].ToString());
                            else if (dr["con_type"].ToString() == EbConnectionTypes.SMTP.ToString())
                                cons.SMTPConnection = EbSerializers.Json_Deserialize<SMTPConnection>(dr["con_obj"].ToString());
                            else if (dr["con_type"].ToString() == EbConnectionTypes.SMS.ToString())
                                cons.SMSConnection = EbSerializers.Json_Deserialize<SMSConnection>(dr["con_obj"].ToString());
                            // ... More to come
                        }

                        Redis.Set<EbConnectionsConfig>(string.Format(CoreConstants.SOLUTION_CONNECTION_REDIS_KEY, req.TenantAccountId), cons);
                        resp.EBSolutionConnections = cons;
                    }
                }
            return resp;
        }

        [Authenticate]
        public void Post(InitialSolutionConnectionsRequest request)
        {
            EbConnectionsConfig _solutionConnections = EbConnectionsConfigProvider.DataCenterConnections;

            _solutionConnections.ObjectsDbConnection.DatabaseName = request.SolutionId;
            _solutionConnections.ObjectsDbConnection.NickName = request.SolutionId + "_Initial";

            _solutionConnections.DataDbConnection.DatabaseName = request.SolutionId;
            _solutionConnections.DataDbConnection.NickName = request.SolutionId + "_Initial";

            _solutionConnections.ObjectsDbConnection.Persist(request.SolutionId, this.InfraConnectionFactory, true, request.UserId);
            _solutionConnections.DataDbConnection.Persist(request.SolutionId, this.InfraConnectionFactory, true, request.UserId);
            _solutionConnections.FilesDbConnection.Persist(request.SolutionId, this.InfraConnectionFactory, true, request.UserId);

            this.Redis.Set<EbConnectionsConfig>(string.Format(CoreConstants.SOLUTION_CONNECTION_REDIS_KEY, request.SolutionId), _solutionConnections);
        }

        [Authenticate]
        public void Post(ChangeSMTPConnectionRequest request)
        {
            request.SMTPConnection.Persist(request.TenantAccountId, this.InfraConnectionFactory, request.IsNew, request.UserId);
            base.MessageProducer3.Publish(new RefreshSolutionConnectionsRequest() { TenantAccountId = request.TenantAccountId, UserId = request.UserId });
        }

        [Authenticate]
        public ChangeConnectionResponse Post(ChangeDataDBConnectionRequest request)
        {
            ChangeConnectionResponse res = new ChangeConnectionResponse();
            try
            {
                request.DataDBConnection.Persist(request.SolutionId, this.InfraConnectionFactory, request.IsNew, request.UserId);

                var myService = base.ResolveService<EbDbCreateServices>();
                var result = myService.Post(new EbDbCreateRequest() { dbName = request.DataDBConnection.DatabaseName, ischange = true, DataDBConnection = request.DataDBConnection, UserId = request.UserId, TenantAccountId = request.TenantAccountId });
                base.MessageProducer3.Publish(new RefreshSolutionConnectionsRequest()
                {
                    TenantAccountId = request.SolutionId,
                    UserId = request.UserId,
                    BToken = (!String.IsNullOrEmpty(this.Request.Authorization)) ? this.Request.Authorization.Replace("Bearer", string.Empty).Trim() : String.Empty,
                    RToken = (!String.IsNullOrEmpty(this.Request.Headers["rToken"])) ? this.Request.Headers["rToken"] : String.Empty
                });
            }
            catch (Exception e)
            {
                res.ResponseStatus.Message = e.Message;
            }

            return res;
        }

        [Authenticate]
        public ChangeConnectionResponse Post(ChangeObjectsDBConnectionRequest request)
        {
            ChangeConnectionResponse res = new ChangeConnectionResponse();
            try
            {
                request.ObjectsDBConnection.Persist(request.SolutionId, this.InfraConnectionFactory, request.IsNew, request.UserId);
                base.MessageProducer3.Publish(new RefreshSolutionConnectionsRequest() { TenantAccountId = request.SolutionId, UserId = request.UserId });
            }
            catch (Exception e)
            {
                res.ResponseStatus.Message = e.Message;
            }
            return res;
        }

        [Authenticate]
        public ChangeConnectionResponse Post(ChangeFilesDBConnectionRequest request)
        {
            ChangeConnectionResponse res = new ChangeConnectionResponse();
            try
            {
                request.FilesDBConnection.Persist(request.TenantAccountId, this.InfraConnectionFactory, request.IsNew, request.UserId);
                base.MessageProducer3.Publish(new RefreshSolutionConnectionsRequest() { TenantAccountId = request.TenantAccountId, UserId = request.UserId });
            }
            catch (Exception e)
            {
                res.ResponseStatus.Message = e.Message;
            }
            return res;
        }

        [Authenticate]
        public ChangeConnectionResponse Post(ChangeSMSConnectionRequest request)
        {
            ChangeConnectionResponse res = new ChangeConnectionResponse();
            try
            {
                request.SMSConnection.Persist(request.TenantAccountId, this.InfraConnectionFactory, request.IsNew, request.UserId);
                base.MessageProducer3.Publish(new RefreshSolutionConnectionsRequest() { TenantAccountId = request.TenantAccountId, UserId = request.UserId });
            }
            catch (Exception e)
            {
                res.ResponseStatus.Message = e.Message;
            }
            return res;
        }

        public TestConnectionResponse Post(TestConnectionRequest request)
        {
            TestConnectionResponse res = new TestConnectionResponse();
            bool IsAdmin = false;
            IDatabase DataDB = null;
            if (request.DataDBConnection.DatabaseVendor == DatabaseVendors.PGSQL)
                DataDB = new PGSQLDatabase(request.DataDBConnection);
            else if (request.DataDBConnection.DatabaseVendor == DatabaseVendors.ORACLE)
                DataDB = new OracleDB(request.DataDBConnection);

            try
            {
                var dt = DataDB.DoQuery(DataDB.EB_USER_ROLE_PRIVS.Replace("@uname", request.DataDBConnection.UserName));

                if (request.DataDBConnection.DatabaseVendor == DatabaseVendors.PGSQL)
                {
                    string[] adminroles = Enum.GetNames(typeof(PGSQLSysRoles));
                    List<string> adroleslist = adminroles.OfType<string>().ToList();
                    foreach (var dr in dt.Rows)
                    {
                        if (adroleslist.Contains(dr[0]))     //IsAdmin = (adroleslist.Contains(dr[0])) ? true : false;
                            IsAdmin = true;
                        else
                        {
                            IsAdmin = false;
                            break;
                        }
                    }
                    res.ConnectionStatus = IsAdmin;

                }
                else if (request.DataDBConnection.DatabaseVendor == DatabaseVendors.ORACLE)
                {
                    string[] adminroles = Enum.GetNames(typeof(OracleSysRoles));
                    List<string> adroleslist = adminroles.OfType<string>().ToList();
                    foreach (var dr in dt.Rows)
                    {
                        if (adroleslist.Contains(dr[0]))
                            IsAdmin = true;
                        else
                        {
                            IsAdmin = false;
                            break;
                        }

                    }
                    res.ConnectionStatus = IsAdmin;

                }

            }
            catch (Exception e)
            {
                Console.WriteLine("Exception:" + e.ToString());
                res.ConnectionStatus = IsAdmin;
            }

            return res;
        }

        public TestFileDbconnectionResponse Post(TestFileDbconnectionRequest request)
        {
            TestFileDbconnectionResponse res = new TestFileDbconnectionResponse();
            try
            {
                MongoDBDatabase mongo = new MongoDBDatabase(request.UserId.ToString(), request.FilesDBConnection);
                res.ConnectionStatus = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception:" + e.ToString());
                res.ConnectionStatus = false;
            }
            return res;
        }
    }
}
