﻿using System.Collections.Generic;
using ServiceStack;
using ExpressBase.Data;
using System;
using ExpressBase.Objects;
using System.Data.Common;
using ExpressBase.Objects.ServiceStack_Artifacts;
using ServiceStack.Logging;

namespace ExpressBase.ServiceStack
{
    [ClientCanSwapTemplates]
    [DefaultView("Form")]
    public class EbObjectService : EbBaseService
    {
        #region Get EbObject Queries

        // Fetch all version without bytea of a particular Object
        private const string Query1 = @"
SELECT 
    EOV.id, EOV.ver_num, EOV.obj_changelog, EOV.commit_ts, EU.firstname
FROM 
    eb_objects_ver EOV, eb_users EU
WHERE
    EOV.commit_uid = EU.id AND
    EOV.eb_objects_id=@id
ORDER BY
    ver_num DESC";

        // Fetch particular version with Bytea of a particular Object
        private const string Query2 = "SELECT obj_bytea FROM eb_objects_ver WHERE id=@id";

        // Fetch latest non-committed version with bytea - for EDIT
        private const string Query3 = @"
SELECT 
    EO.id, EO.obj_name, EO.obj_type, EO.obj_last_ver_id, EO.obj_cur_status,EO.obj_desc,
    EOV.id,EOV.eb_objects_id,EOV.ver_num, EOV.obj_changelog,EOV.commit_ts, EOV.commit_uid, EOV.obj_bytea
FROM 
    eb_objects EO, eb_objects_ver EOV
WHERE
    EO.id = EOV.eb_objects_id AND EO.id=@id AND EOV.ver_num = -1 AND EOV.commit_uid IS NULL
ORDER BY
    EO.obj_type";

        // Fetch latest committed version with bytea - for Execute/Run/Consume
        private const string Query4 = @"
SELECT 
    EO.id, EO.obj_name, EO.obj_type, EO.obj_last_ver_id, EO.obj_cur_status,EO.obj_desc,
    EOV.id,EOV.eb_objects_id,EOV.ver_num, EOV.obj_changelog,EOV.commit_ts, EOV.commit_uid, EOV.obj_bytea
FROM 
    eb_objects EO, eb_objects_ver EOV
WHERE
    EO.id = EOV.eb_objects_id AND EO.id=@id AND EOV.ver_num = EO.obj_last_ver_id AND EOV.commit_uid IS NOT NULL
ORDER BY
    EO.obj_type";

        // Get All latest committed versions of this Object Type without Bytea
        private const string Query5 = @"
SELECT 
    EO.id, EO.obj_name, EO.obj_type, EO.obj_last_ver_id, EO.obj_cur_status,EO.obj_desc,
    EOV.id, EOV.eb_objects_id, EOV.ver_num, EOV.obj_changelog,EOV.commit_ts, EOV.commit_uid,
    EU.firstname
FROM 
    eb_objects EO, eb_objects_ver EOV,eb_users EU
WHERE
    EO.id = EOV.eb_objects_id AND EO.obj_last_ver_id=EOV.ver_num AND EO.obj_type=@type AND EOV.commit_uid IS NOT NULL AND  EOV.commit_uid = EU.id
ORDER BY
    EO.obj_name";

        #endregion

        [Authenticate]
        [CompressResponse]
        public object Get(EbObjectRequest request)
        {
            base.ClientID = request.TenantAccountId;

            List<EbObjectWrapper> f = new List<EbObjectWrapper>();
            ILog log = LogManager.GetLogger(GetType());
            List<System.Data.Common.DbParameter> parameters = new List<System.Data.Common.DbParameter>();

            // Fetch all version without bytea of a particular Object
            if (request.Id > 0 && request.VersionId == 0)
            {
                parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.Id));
                var dt = this.DatabaseFactory.ObjectsDB.DoQuery(Query1, parameters.ToArray());

                foreach (EbDataRow dr in dt.Rows)
                {
                    var _ebObject = (new EbObjectWrapper
                    {
                        Id = Convert.ToInt32(dr[0]),
                        VersionNumber = Convert.ToInt32(dr[1]),
                        ChangeLog = dr[2].ToString(),
                        CommitTs = Convert.ToDateTime(dr[3]),
                        CommitUname = dr[4].ToString()
                    });
                    f.Add(_ebObject);
                }
            }

            // Fetch particular version with Bytea of a particular Object
            if (request.VersionId > 0 && request.VersionId < Int32.MaxValue)
            {
                parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.VersionId));
                var dt = this.DatabaseFactory.ObjectsDB.DoQuery(Query2, parameters.ToArray());

                foreach (EbDataRow dr in dt.Rows)
                {
                    var _ebObject = (new EbObjectWrapper
                    {
                        Bytea = dr[0] as byte[]
                    });
                    f.Add(_ebObject);
                }
            }

            // Fetch latest non-committed version with bytea - for EDIT of a particular Object
            if (request.Id > 0 && request.VersionId < 0)
            {
                parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.Id));
                var dt = this.DatabaseFactory.ObjectsDB.DoQuery(Query3, parameters.ToArray());

                foreach (EbDataRow dr in dt.Rows)
                {
                    var _ebObject = (new EbObjectWrapper
                    {
                        Id = Convert.ToInt32(dr[0]),
                        Name = dr[1].ToString(),
                        EbObjectType = (EbObjectType)Convert.ToInt32(dr[2]),
                        Status = (ObjectLifeCycleStatus)dr[4],
                        Description = dr[5].ToString(),
                        VersionNumber = Convert.ToInt32(dr[3]),
                        Bytea = (request.Id > 0) ? dr[12] as byte[] : null
                    });

                    f.Add(_ebObject);
                }
            }

            // Fetch latest committed version with bytea - for Execute/Run/Consume a particular Object
            if (request.Id > 0 && request.VersionId == Int32.MaxValue)
            {
                parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.Id));
                var dt = this.DatabaseFactory.ObjectsDB.DoQuery(Query4, parameters.ToArray());

                foreach (EbDataRow dr in dt.Rows)
                {
                    var _ebObject = (new EbObjectWrapper
                    {
                        Id = Convert.ToInt32(dr[0]),
                        Name = dr[1].ToString(),
                        EbObjectType = (EbObjectType)Convert.ToInt32(dr[2]),
                        Status = (ObjectLifeCycleStatus)dr[4],
                        Description = dr[5].ToString(),
                        VersionNumber = Convert.ToInt32(dr[8]),
                        Bytea = (request.Id > 0) ? dr[12] as byte[] : null
                    });

                    f.Add(_ebObject);
                }
            }

            // Get All latest committed versions of this Object Type without Bytea
            if (request.Id == 0 && request.VersionId == Int32.MaxValue)
            {
                parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@type", System.Data.DbType.Int32, request.EbObjectType));
                var dt = this.DatabaseFactory.ObjectsDB.DoQuery(Query5, parameters.ToArray());

                foreach (EbDataRow dr in dt.Rows)
                {
                    var _ebObject = (new EbObjectWrapper
                    {
                        Id = Convert.ToInt32(dr[0]),
                        Name = dr[1].ToString(),
                        EbObjectType = (EbObjectType)Convert.ToInt32(dr[2]),
                        Status = (ObjectLifeCycleStatus)dr[4],
                        Description = dr[5].ToString(),
                        VersionNumber = Convert.ToInt32(dr[8]),
                        CommitTs = Convert.ToDateTime(dr[10]),
                        CommitUname = dr[12].ToString(),
                    });

                    f.Add(_ebObject);
                }
            }

            return new EbObjectResponse { Data = f };
        }

        #region SaveOrCommit Queries

        private const string Query_FirstCommit = @"
INSERT INTO eb_objects 
    (obj_name, obj_desc, obj_type, obj_last_ver_id, obj_cur_status) 
VALUES
    (@obj_name, @obj_desc, @obj_type, 1, @obj_cur_status)  RETURNING id;

INSERT INTO eb_objects_ver
    (eb_objects_id, ver_num, obj_bytea, commit_uid, commit_ts) 
VALUES
    (CURRVAL('eb_objects_id_seq'), 1, @obj_bytea, @commit_uid, NOW());

INSERT INTO eb_objects_ver
    (eb_objects_id, ver_num, obj_bytea) 
VALUES
    (CURRVAL('eb_objects_id_seq'), -1, @obj_bytea);

INSERT INTO eb_objects_relations
    (dominant,dependant)
VALUES
    (UNNEST(@relations),CURRVAL('eb_objects_id_seq'))
";

        private const string Query_SubsequentCommit = @"
UPDATE eb_objects 
SET 
    obj_name=@obj_name, obj_desc=@obj_desc, 
    obj_last_ver_id=(SELECT MAX(ver_num)+1 FROM eb_objects_ver WHERE eb_objects_id=@id), 
    obj_cur_status=@obj_cur_status 
WHERE 
    id=@id RETURNING id; 

UPDATE eb_objects_ver
SET
    obj_bytea=@obj_bytea, obj_changelog=@obj_changelog, ver_num=(SELECT MAX(ver_num)+1 FROM eb_objects_ver WHERE eb_objects_id=@id), commit_uid=@commit_uid, commit_ts=NOW()
WHERE
    eb_objects_id=@id AND commit_uid IS NULL AND ver_num=-1;

INSERT INTO eb_objects_ver
    (eb_objects_id, ver_num, obj_bytea) 
VALUES
    (@id, -1, @obj_bytea)";

        private const string Query_Save = @"
UPDATE eb_objects SET obj_name=@obj_name, obj_desc=@obj_desc WHERE id=@id;
UPDATE eb_objects_ver SET obj_bytea=@obj_bytea WHERE eb_objects_id=@id AND commit_uid IS NULL AND ver_num=-1;";

        #endregion

        [Authenticate]
        public EbObjectSaveOrCommitResponse Post(EbObjectSaveOrCommitRequest request)
        {
            base.ClientID = request.TenantAccountId;

            ILog log = LogManager.GetLogger(GetType());
            log.Info("#DS insert -- entered post");

            using (var con = this.DatabaseFactory.ObjectsDB.GetNewConnection())
            {
                con.Open();
                DbCommand cmd = null;
                log.Info("#DS insert 1 -- con open");

                // First COMMIT
                if (!request.IsSave && request.Id == 0)
                {
                    cmd = this.DatabaseFactory.ObjectsDB.GetNewCommand(con, Query_FirstCommit);
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_type", System.Data.DbType.Int32, (int)request.EbObjectType));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_name", System.Data.DbType.String, request.Name));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_desc", System.Data.DbType.String, request.Description));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_bytea", System.Data.DbType.Binary, request.Bytea));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_cur_status", System.Data.DbType.Int32, ObjectLifeCycleStatus.Development));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@commit_uid", System.Data.DbType.Int32, request.UserId));
                    cmd.Parameters.Add(base.DatabaseFactory.ObjectsDB.GetNewParameter("@relations", NpgsqlTypes.NpgsqlDbType.Array, request.Relations.Split(',').ToArray()));
                    }

                // Subsequent COMMIT
                if (!request.IsSave && request.Id > 0)
                {
                    cmd = this.DatabaseFactory.ObjectsDB.GetNewCommand(con, Query_SubsequentCommit);
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.Id));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_name", System.Data.DbType.String, request.Name));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_desc", System.Data.DbType.String, request.Description));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_bytea", System.Data.DbType.Binary, request.Bytea));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_cur_status", System.Data.DbType.Int32, ObjectLifeCycleStatus.Development));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_changelog", System.Data.DbType.String, request.ChangeLog));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@commit_uid", System.Data.DbType.Int32, request.UserId));
                }

                // SAVE
                if (request.IsSave)
                {
                    cmd = this.DatabaseFactory.ObjectsDB.GetNewCommand(con, Query_Save);
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.Id));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_name", System.Data.DbType.String, request.Name));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_desc", System.Data.DbType.String, request.Description));
                    cmd.Parameters.Add(this.DatabaseFactory.ObjectsDB.GetNewParameter("@obj_bytea", System.Data.DbType.Binary, request.Bytea));
                }

                return new EbObjectSaveOrCommitResponse() { Id = Convert.ToInt32(cmd.ExecuteScalar()) };
            };
        }
    }
}