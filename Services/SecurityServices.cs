﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpressBase.Objects.ServiceStack_Artifacts;
using ExpressBase.Common.Data;
using System.Data.Common;
using ExpressBase.Common;
using ExpressBase.Security.Core;
using ExpressBase.Common.Extensions;

namespace ExpressBase.ServiceStack.Services
{
    public class SecurityServices : EbBaseService
	{
		public SecurityServices(ITenantDbFactory _dbf) : base(_dbf) { }

		//------COMMON LIST-------------------------------------

		public GetUsersResponse1 Any(GetUsersRequest1 request)
		{
			GetUsersResponse1 resp = new GetUsersResponse1();
			using (var con = this.TenantDbFactory.ObjectsDB.GetNewConnection())
			{
				con.Open();
				string sql = "SELECT id,firstname,email FROM eb_users WHERE firstname ~* @searchtext";

				DbParameter[] parameters = { this.TenantDbFactory.ObjectsDB.GetNewParameter("searchtext", System.Data.DbType.String, (request.Colvalues != null) ? request.Colvalues["searchtext"] : string.Empty) };

				var dt = this.TenantDbFactory.ObjectsDB.DoQueries(sql, parameters);

				List<Eb_User_ForCommonList> returndata = new List<Eb_User_ForCommonList>();
				foreach (EbDataRow dr in dt.Tables[0].Rows)
				{
					returndata.Add(new Eb_User_ForCommonList {Id = Convert.ToInt32(dr[0]), Name = dr[1].ToString(), Email = dr[2].ToString() });
				}
				resp.Data = returndata;
			}
			return resp;
		} //for user search

		public GetUserGroupResponse1 Any(GetUserGroupRequest1 request)
		{
			GetUserGroupResponse1 resp = new GetUserGroupResponse1();
			using (var con = this.TenantDbFactory.ObjectsDB.GetNewConnection())
			{
				con.Open();
				string sql = "SELECT id,name,description FROM eb_usergroup WHERE name ~* @searchtext";

				DbParameter[] parameters = { this.TenantDbFactory.ObjectsDB.GetNewParameter("searchtext", System.Data.DbType.String, (request.Colvalues != null) ? request.Colvalues["searchtext"] : string.Empty) };

				var dt = this.TenantDbFactory.ObjectsDB.DoQueries(sql, parameters);

				List<Eb_UserGroup_ForCommonList> returndata = new List<Eb_UserGroup_ForCommonList>();
				foreach (EbDataRow dr in dt.Tables[0].Rows)
				{
					returndata.Add(new Eb_UserGroup_ForCommonList { Id = Convert.ToInt32(dr[0]), Name = dr[1].ToString(), Description = dr[2].ToString() });
				}
				resp.Data = returndata;
			}
			return resp;
		} //for usergroup search

		public GetRolesResponse1 Any(GetRolesRequest1 request)
		{
			GetRolesResponse1 resp = new GetRolesResponse1();
			using (var con = this.TenantDbFactory.ObjectsDB.GetNewConnection())
			{
				con.Open();
				string sql = "SELECT id,role_name,description FROM eb_roles WHERE role_name ~* @searchtext";

				DbParameter[] parameters = { this.TenantDbFactory.ObjectsDB.GetNewParameter("searchtext", System.Data.DbType.String, (request.Colvalues != null) ? request.Colvalues["searchtext"] : string.Empty) };

				var dt = this.TenantDbFactory.ObjectsDB.DoQueries(sql, parameters);

				List<Eb_Roles_ForCommonList> returndata = new List<Eb_Roles_ForCommonList>();
				foreach (EbDataRow dr in dt.Tables[0].Rows)
				{
					returndata.Add(new Eb_Roles_ForCommonList { Id = Convert.ToInt32(dr[0]), Name = dr[1].ToString(), Description = dr[2].ToString() });
				}
				resp.Data = returndata;
			}
			return resp;
		} //for roles search



		//----MANAGE USER START---------------------------------
		public GetManageUserResponse Any(GetManageUserRequest request)
		{
			GetManageUserResponse resp = new GetManageUserResponse();
			string sql = @"SELECT id, role_name, description FROM eb_roles ORDER BY role_name;
                        SELECT id, name,description FROM eb_usergroup ORDER BY name;
						SELECT id, role1_id, role2_id FROM eb_role2role WHERE eb_del = FALSE;";
			if (request.Id > 0)
			{
				sql += @"SELECT fullname,nickname,email,alternateemail,dob,sex,phnoprimary,phnosecondary,landline,phextension,fbid,fbname,statusid,hide
						FROM eb_users WHERE id = @id;
						SELECT role_id FROM eb_role2user WHERE user_id = @id AND eb_del = FALSE;
						SELECT groupid FROM eb_user2usergroup WHERE userid = @id AND eb_del = FALSE;";
			}
			//SELECT firstname, email, socialid, socialname FROM eb_users WHERE id = @id;	old 4th query
			DbParameter[] parameters = { this.TenantDbFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.Id) };
			var ds = this.TenantDbFactory.ObjectsDB.DoQueries(sql, parameters);

			resp.Roles = new List<EbRole>();
			foreach (var dr in ds.Tables[0].Rows)
			{
				resp.Roles.Add(new EbRole
				{
					Id = Convert.ToInt32(dr[0]),
					Name = dr[1].ToString(),
					Description = dr[2].ToString()
				});
			}

			resp.EbUserGroups = new List<EbUserGroups>();
			foreach (var dr in ds.Tables[1].Rows)
			{
				resp.EbUserGroups.Add(new EbUserGroups
				{
					Id = Convert.ToInt32(dr[0]),
					Name = dr[1].ToString(),
					Description = dr[2].ToString()

				});
			}

			resp.Role2RoleList = new List<Eb_RoleToRole>();
			foreach (EbDataRow dr in ds.Tables[2].Rows)
			{
				resp.Role2RoleList.Add(new Eb_RoleToRole() { Id = Convert.ToInt32(dr[0]), Dominant = Convert.ToInt32(dr[1]), Dependent = Convert.ToInt32(dr[2]) });
			}


			if (request.Id > 0)
			{
				resp.UserData = new Dictionary<string, string>();
				foreach (var dr in ds.Tables[3].Rows)
				{
					resp.UserData.Add("id", request.Id.ToString());
					resp.UserData.Add("fullname", dr[0].ToString());
					resp.UserData.Add("nickname", dr[1].ToString());
					resp.UserData.Add("email", dr[2].ToString());
					resp.UserData.Add("alternateemail", dr[3].ToString());
					resp.UserData.Add("dob", dr[4].ToString().Substring(0, 10));
					resp.UserData.Add("sex", dr[5].ToString());
					resp.UserData.Add("phnoprimary", dr[6].ToString());
					resp.UserData.Add("phnosecondary", dr[7].ToString());
					resp.UserData.Add("landline", dr[8].ToString());
					resp.UserData.Add("phextension", dr[9].ToString());
					resp.UserData.Add("fbid", dr[10].ToString());
					resp.UserData.Add("fbname", dr[11].ToString());
					resp.UserData.Add("statusid", dr[12].ToString());
					resp.UserData.Add("hide", dr[13].ToString());
				}

				resp.UserRoles = new List<int>();
				foreach (var dr in ds.Tables[4].Rows)
					resp.UserRoles.Add(Convert.ToInt32(dr[0]));

				resp.UserGroups = new List<int>();
				foreach (var dr in ds.Tables[5].Rows)
					resp.UserGroups.Add(Convert.ToInt32(dr[0]));
			}

			return resp;
		}

		private string GeneratePassword()
		{
			string strPwdchar = "abcdefghijklmnopqrstuvwxyz0123456789#+@&$ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			string strPwd = "";
			Random rnd = new Random();
			for (int i = 0; i <= 7; i++)
			{
				int iRandom = rnd.Next(0, strPwdchar.Length - 1);
				strPwd += strPwdchar.Substring(iRandom, 1);
			}
			return strPwd;
		}

		public bool Any(UniqueCheckRequest request)
		{
			
			string sql = "SELECT id FROM eb_users WHERE email LIKE @email";
			DbParameter[] parameters = { this.TenantDbFactory.ObjectsDB.GetNewParameter("email", System.Data.DbType.String, request.email) };
			var dt = this.TenantDbFactory.ObjectsDB.DoQuery(sql, parameters);
			if (dt.Rows.Count > 0)
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		public SaveUserResponse Post(SaveUserRequest request)
		{
			SaveUserResponse resp;
			string sql = "";
			using (var con = this.TenantDbFactory.ObjectsDB.GetNewConnection())
			{
				con.Open();
				string password = "";

				if (request.Id > 0)
				{
					sql = "SELECT * FROM eb_createormodifyuserandroles(@userid,@id,@fullname,@nickname,@email,@pwd,@dob,@sex,@alternateemail,@phprimary,@phsecondary,@phlandphone,@extension,@fbid,@fbname,@roles,@group,@statusid,@hide);";

				}
				else
				{
					//password = string.IsNullOrEmpty(request.Colvalues["pwd"].ToString()) ? GeneratePassword() : (request.Colvalues["pwd"].ToString() + request.Colvalues["email"].ToString()).ToMD5Hash();
					password = GeneratePassword();
					sql = "SELECT * FROM eb_createormodifyuserandroles(@userid,@id,@fullname,@nickname,@email,@pwd,@dob,@sex,@alternateemail,@phprimary,@phsecondary,@phlandphone,@extension,@fbid,@fbname,@roles,@group,@statusid,@hide);";

				}
				int[] emptyarr = new int[] { };
				DbParameter[] parameters = 
					{
						this.TenantDbFactory.ObjectsDB.GetNewParameter("userid", System.Data.DbType.Int32, request.UserId),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("id", System.Data.DbType.Int32, request.Id),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("fullname", System.Data.DbType.String, request.FullName),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("nickname", System.Data.DbType.String, request.NickName),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("email", System.Data.DbType.String, request.EmailPrimary),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("pwd", System.Data.DbType.String,password),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("dob", System.Data.DbType.Date, request.DateOfBirth),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("sex", System.Data.DbType.String, request.Sex),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("alternateemail", System.Data.DbType.String, request.EmailSecondary),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("phprimary", System.Data.DbType.String, request.PhonePrimary),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("phsecondary", System.Data.DbType.String, request.PhoneSecondary),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("phlandphone", System.Data.DbType.String, request.LandPhone),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("extension", System.Data.DbType.String, request.PhoneExtension),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("fbid", System.Data.DbType.String, request.FbId),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("fbname", System.Data.DbType.String, request.FbName),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("roles", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer,(request.Roles != string.Empty? request.Roles.Split(',').Select(n => Convert.ToInt32(n)).ToArray():emptyarr)),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("group", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer,(request.UserGroups != string.Empty? request.UserGroups.Split(',').Select(n => Convert.ToInt32(n)).ToArray():emptyarr)),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("statusid", System.Data.DbType.Int32, Convert.ToInt32(request.StatusId)),
						this.TenantDbFactory.ObjectsDB.GetNewParameter("hide", System.Data.DbType.String, request.Hide)
					};
				
				EbDataSet dt = this.TenantDbFactory.ObjectsDB.DoQueries(sql, parameters);
				
				

				//if (string.IsNullOrEmpty(request.Colvalues["pwd"].ToString()) && request.Id < 0)
				//{
				//	using (var service = base.ResolveService<EmailService>())
				//	{
				//		//  service.Post(new EmailServicesRequest() { To = request.Colvalues["email"].ToString(), Subject = "New User", Message = string.Format("You are invited to join as user. Log in {0}.localhost:53431 using Username: {1} and Password : {2}", request.TenantAccountId, request.Colvalues["email"].ToString(), dt.Tables[0].Rows[0][1]) });
				//	}
				//}
				resp = new SaveUserResponse
				{
					id = Convert.ToInt32(dt.Tables[0].Rows[0][0])

				};
			}
			return resp;
		}

		//------MANAGE USER GROUP START------------------------------

		public GetManageUserGroupResponse Any(GetManageUserGroupRequest request)
		{
			GetManageUserGroupResponse resp = new GetManageUserGroupResponse();
			using (var con = this.TenantDbFactory.ObjectsDB.GetNewConnection())
			{
				con.Open();
				string sql = "";
				if (request.id > 0)
				{
					sql = @"SELECT id,name,description FROM eb_usergroup WHERE id = @id;
                           SELECT id,firstname FROM eb_users WHERE id IN(SELECT userid FROM eb_user2usergroup WHERE groupid = @id AND eb_del = FALSE)";


					DbParameter[] parameters = { this.TenantDbFactory.ObjectsDB.GetNewParameter("id", System.Data.DbType.Int32, request.id) };

					var ds = this.TenantDbFactory.ObjectsDB.DoQueries(sql, parameters);
					Dictionary<string, object> result = new Dictionary<string, object>();
					foreach (var dr in ds.Tables[0].Rows)
					{

						result.Add("name", dr[1].ToString());
						result.Add("description", dr[2].ToString());
					}
					List<int> users = new List<int>();
					if (ds.Tables.Count > 1)
					{
						foreach (EbDataRow dr in ds.Tables[1].Rows)
						{
							users.Add(Convert.ToInt32(dr[0]));
							result.Add(dr[0].ToString(), dr[1]);
						}
						result.Add("userslist", users);
					}
					resp.Data = result;
				}
				else
				{
					sql = "SELECT id,name FROM eb_usergroup";
					var dt = this.TenantDbFactory.ObjectsDB.DoQueries(sql);

					Dictionary<string, object> returndata = new Dictionary<string, object>();
					foreach (EbDataRow dr in dt.Tables[0].Rows)
					{
						returndata[dr[0].ToString()] = dr[1].ToString();
					}
					resp.Data = returndata;
				}

			}
			return resp;
		}

		//----MANAGE ROLES START---------------------------------------
		public GetManageRolesResponse Any(GetManageRolesRequest request)
		{
			string query = null;
			List<DbParameter> parameters = new List<DbParameter>();
			//SELECT DISTINCT EO.id, EO.obj_name, EO.obj_type, EO.applicationid
			//FROM eb_objects EO, eb_objects_ver EOV, eb_objects_status EOS
			//WHERE EO.id = EOV.eb_objects_id AND EOV.id = EOS.eb_obj_ver_id AND EOS.status = 3 AND EO.applicationid > 0;
			query = string.Format(@"SELECT id, applicationname FROM eb_applications where eb_del = FALSE ORDER BY applicationname;

									SELECT DISTINCT EO.id, EO.obj_name, EO.obj_type, EO2A.app_id
									FROM eb_objects EO, eb_objects_ver EOV, eb_objects_status EOS, eb_objects2application EO2A 
									WHERE EO.id = EOV.eb_objects_id AND EOV.id = EOS.eb_obj_ver_id AND EOS.status = 3 
									AND EO.id = EO2A.obj_id AND EO2A.eb_del = 'false';

									SELECT id, role_name, description, applicationid FROM eb_roles WHERE id <> @id ORDER BY role_name;
									SELECT id, role1_id, role2_id FROM eb_role2role WHERE eb_del = FALSE;");//if db_ok then append to 3rd query "WHERE eb_del=FALSE" 
			if (request.id > 0)
			{
				query += string.Format(@"SELECT role_name,applicationid,description FROM eb_roles WHERE id = @id;
										SELECT permissionname,obj_id,op_id FROM eb_role2permission WHERE role_id = @id AND eb_del = FALSE;
										SELECT A.applicationname, A.description FROM eb_applications A, eb_roles R WHERE A.id = R.applicationid AND R.id = @id AND A.eb_del = FALSE;

										SELECT A.id, A.firstname, A.email, B.id FROM eb_users A, eb_role2user B
											WHERE A.id = B.user_id AND A.eb_del = FALSE AND B.eb_del = FALSE AND B.role_id = @id");
			}
			parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("@id", System.Data.DbType.Int32, request.id));
			var ds = this.TenantDbFactory.ObjectsDB.DoQueries(query, parameters.ToArray());
			ApplicationCollection _applicationCollection = null;
			List<Eb_RoleObject> _roleList = new List<Eb_RoleObject>();
			List<Eb_RoleToRole> _r2rList = new List<Eb_RoleToRole>();
			
			if (ds.Tables.Count > 0)
			{
				//PROCESSED RESULT
				_applicationCollection = new ApplicationCollection(ds.Tables[0], ds.Tables[1]);
				//---------------
				foreach (EbDataRow dr in ds.Tables[2].Rows)
				{
					_roleList.Add(new Eb_RoleObject() { Id = Convert.ToInt32(dr[0]), Name = dr[1].ToString(), Description = dr[2].ToString(), App_Id = Convert.ToInt32(dr[3]) });
				}
				foreach (EbDataRow dr in ds.Tables[3].Rows)
				{
					_r2rList.Add(new Eb_RoleToRole() { Id = Convert.ToInt32(dr[0]), Dominant= Convert.ToInt32(dr[1]), Dependent= Convert.ToInt32(dr[2])});
				}

			}
			Dictionary<string, object> RoleInfo = new Dictionary<string, object>();
			List<string> Permission = new List<string>();
			List<Eb_Users> _usersList = new List<Eb_Users>();

			if (ds.Tables.Count > 4)
			{
				RoleInfo.Add("RoleName", ds.Tables[4].Rows[0][0].ToString());
				RoleInfo.Add("AppId", Convert.ToInt32(ds.Tables[4].Rows[0][1]));
				RoleInfo.Add("RoleDescription", ds.Tables[4].Rows[0][2].ToString());
				RoleInfo.Add("AppName", ds.Tables[6].Rows[0][0].ToString());
				RoleInfo.Add("AppDescription", ds.Tables[6].Rows[0][1].ToString());
				foreach (var dr in ds.Tables[5].Rows)
					Permission.Add(dr[0].ToString());
				foreach (EbDataRow dr in ds.Tables[7].Rows)
				{
					_usersList.Add(new Eb_Users() { Id = Convert.ToInt32(dr[0]), Name= dr[1].ToString(),Email= dr[2].ToString(), Role2User_Id= Convert.ToInt32(dr[3]) });
				}
			}
			return new GetManageRolesResponse() { ApplicationCollection = _applicationCollection, SelectedRoleInfo = RoleInfo, PermissionList = Permission, RoleList = _roleList, Role2RoleList = _r2rList, UsersList = _usersList };
		}
		
		public GetUserDetailsResponse Any(GetUserDetailsRequest request)
		{
			string query = null;
			List<DbParameter> parameters = new List<DbParameter>();
			query = string.Format(@"SELECT id, firstname, email FROM eb_users
									WHERE LOWER(firstname) LIKE LOWER(@NAME) AND eb_del = FALSE ORDER BY firstname ASC"); 
			parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("@NAME", System.Data.DbType.String, ("%" + request.SearchText + "%")));
			var ds = this.TenantDbFactory.ObjectsDB.DoQueries(query, parameters.ToArray());
			List<Eb_Users> _usersList = new List<Eb_Users>();
			if (ds.Tables.Count > 0)
			{
				foreach (EbDataRow dr in ds.Tables[0].Rows)
				{
					_usersList.Add(new Eb_Users() { Id = Convert.ToInt32(dr[0]), Name = dr[1].ToString(), Email = dr[2].ToString()});
				}
			}
			return new GetUserDetailsResponse() { UserList = _usersList };
		}
		
		public SaveRoleResponse Post(SaveRoleRequest request)
		{
			SaveRoleResponse resp;
			using (var con = this.TenantDbFactory.ObjectsDB.GetNewConnection())
			{
				con.Open();
				string sql = "SELECT eb_create_or_update_rbac_manageroles(@role_id, @applicationid, @createdby, @role_name, @description, @users, @dependants,@permission );";
				var cmd = this.TenantDbFactory.ObjectsDB.GetNewCommand(con, sql);
				int[] emptyarr = new int[] { };
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("role_id", System.Data.DbType.Int32, request.Colvalues["roleid"]));
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("description", System.Data.DbType.String, request.Colvalues["Description"]));
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("role_name", System.Data.DbType.String, request.Colvalues["role_name"]));
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("applicationid", System.Data.DbType.Int32, request.Colvalues["applicationid"]));
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("createdby", System.Data.DbType.Int32, request.UserId));
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("permission", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text, (request.Colvalues["permission"].ToString() != string.Empty) ? request.Colvalues["permission"].ToString().Replace("[", "").Replace("]", "").Split(',').Select(n => n.ToString()).ToArray() : new string[] { }));
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("users", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer, (request.Colvalues["users"].ToString() != string.Empty) ? request.Colvalues["users"].ToString().Split(',').Select(n => Convert.ToInt32(n)).ToArray() : emptyarr));
				cmd.Parameters.Add(this.TenantDbFactory.ObjectsDB.GetNewParameter("dependants", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer, (request.Colvalues["dependants"].ToString() != string.Empty) ? request.Colvalues["dependants"].ToString().Replace("[", "").Replace("]", "").Split(',').Select(n => Convert.ToInt32(n)).ToArray() : emptyarr));

				resp = new SaveRoleResponse
				{
					id = Convert.ToInt32(cmd.ExecuteScalar())
				};
			}
			return resp;
		}
		
	}	

}
