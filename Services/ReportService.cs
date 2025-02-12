﻿
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.Scripting;
using System.Text.RegularExpressions;
using ServiceStack.Redis;
using ServiceStack.Messaging;
using iTextSharp.text.html.simpleparser;
using ServiceStack;
using ExpressBase.Common;
using ExpressBase.Common.Data;
using ExpressBase.Common.ServiceClients;
using ExpressBase.Common.Structures;
using ExpressBase.Objects;
using ExpressBase.Objects.ServiceStack_Artifacts;
using ExpressBase.ServiceStack.Services;
using ExpressBase.CoreBase.Globals;
using ExpressBase.Common.Singletons;
using System.Diagnostics;
using Newtonsoft.Json;
using ExpressBase.Objects.Helpers;

namespace ExpressBase.ServiceStack
{
    [Authenticate]
    public class ReportService : EbBaseService
    {
        //private iTextSharp.text.Font f = FontFactory.GetFont(FontFactory.HELVETICA, 12);
        public ReportService(IEbConnectionFactory _dbf, IEbStaticFileClient _sfc, IMessageProducer _mqp, IMessageQueueClient _mqc) : base(_dbf, _sfc, _mqp, _mqc) { }

        public MemoryStream Ms1 = null;

        public EbReport Report = null;

        public PdfWriter Writer = null;

        public Document Document = null;

        public PdfContentByte Canvas = null;


        public ReportRenderResponse Get(ReportRenderRequest request)
        {
            if (!string.IsNullOrEmpty(request.Refid))
            {
                this.Ms1 = new MemoryStream();
                List<EbObjectWrapper> resultlist = EbObjectsHelper.GetParticularVersion(this.EbConnectionFactory.ObjectsDB, request.Refid);
                Report = EbSerializers.Json_Deserialize<EbReport>(resultlist[0].Json);
                if (Report != null)
                {
                    try
                    {
                        Report.ObjectsDB = this.EbConnectionFactory.ObjectsDB;
                        Report.Redis = this.Redis;
                        Report.FileClient = this.FileClient;
                        Report.Solution = GetSolutionObject(request.SolnId);
                        Report.ReadingUser = GetUserObject(request.ReadingUserAuthId);
                        Report.RenderingUser = GetUserObject(request.RenderingUserAuthId);
                        Report.ExecuteRendering(request.BToken, request.RToken, this.Document, this.Ms1, request.Params, this.EbConnectionFactory);

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception-reportService " + e.Message + e.StackTrace);
                        Report.HandleExceptionPdf();
                    }

                    Report.Doc.Close();

                    Report.SetPassword(Ms1);

                    string name = Report.DocumentName;

                    if (Report.DataSourceRefId != string.Empty && Report.DataSet != null)
                    {
                        Report.DataSet.Tables.Clear();
                        Report.DataSet = null;
                    }

                    this.Ms1.Position = 0;//important

                    return new ReportRenderResponse
                    {
                        StreamWrapper = new MemorystreamWrapper(this.Ms1),
                        ReportName = name,
                        ReportBytea = this.Ms1.ToArray(),
                        CurrentTimestamp = Report.CurrentTimestamp
                    };
                }
            }
            else
            {
                Console.WriteLine("Report render reque reached, but refid is null - " + request.SolnId);
            }
            return null;
        }

        public ReportRenderResponse Get(ReportRenderMultipleRequest request)
        {
            this.MessageProducer3.Publish(new ReportRenderMultipleMQRequest
            {
                RefId = request.Refid,
                Params = request.Params,
                ReadingUserAuthId = request.ReadingUserAuthId,
                RenderingUserAuthId = request.RenderingUserAuthId,
                SolnId = request.SolnId,
                UserAuthId = request.UserAuthId,
                UserId = request.UserId,
                BToken = request.BToken,
                RToken = request.RToken,
                SubscriptionId = request.SubscriptionId
            });

            Console.WriteLine("ReportRenderMultipleMQRequest pushed to MQ Successfully.");
            return new ReportRenderResponse { };
        }

        public ValidateCalcExpressionResponse Get(ValidateCalcExpressionRequest request)
        {
            Type resultType;
            EbDataReader ds = null;
            bool _isValid = true;
            string _excepMsg = string.Empty;
            int resultType_enum = 0;

            EbObjectService myObjectservice = base.ResolveService<EbObjectService>();
            myObjectservice.EbConnectionFactory = this.EbConnectionFactory;
            DataSourceService myDataSourceservice = base.ResolveService<DataSourceService>();
            myDataSourceservice.EbConnectionFactory = this.EbConnectionFactory;

            DataSourceColumnsResponse cresp = new DataSourceColumnsResponse();
            cresp = Redis.Get<DataSourceColumnsResponse>(string.Format("{0}_columns", request.DataSourceRefId));
            if (cresp == null || cresp.Columns.Count == 0)
            {
                ds = Redis.Get<EbDataReader>(request.DataSourceRefId);
                if (ds == null)
                {
                    EbObjectParticularVersionResponse dsresult = myObjectservice.Get(new EbObjectParticularVersionRequest { RefId = request.DataSourceRefId }) as EbObjectParticularVersionResponse;
                    ds = EbSerializers.Json_Deserialize<EbDataReader>(dsresult.Data[0].Json);
                    Redis.Set(request.DataSourceRefId, ds);
                }
                if (ds.FilterDialogRefId != string.Empty)
                    ds.AfterRedisGet(Redis as RedisClient);
                cresp = myDataSourceservice.Any(new DataSourceColumnsRequest { RefId = request.DataSourceRefId, Params = (ds.FilterDialog != null) ? ds.FilterDialog.GetDefaultParams() : null });
                Redis.Set(string.Format("{0}_columns", request.DataSourceRefId), cresp);
            }


            try
            {
                IEnumerable<string> matches = Regex.Matches(request.ValueExpression, @"T[0-9]{1}.\w+").OfType<Match>().Select(m => m.Groups[0].Value).Distinct();

                string[] _dataFieldsUsed = new string[matches.Count()];
                int i = 0;
                foreach (string match in matches)
                    _dataFieldsUsed[i++] = match;

                EbPdfGlobals globals = new EbPdfGlobals();
                {
                    foreach (string calcfd in _dataFieldsUsed)
                    {
                        dynamic _value = null;
                        string TName = calcfd.Split('.')[0];
                        string fName = calcfd.Split('.')[1];
                        EbDbTypes typ = cresp.Columns[Convert.ToInt32(TName.Replace(@"T", string.Empty))][fName].Type;
                        switch (typ.ToString())
                        {
                            case "Int16":
                                _value = 0;
                                break;
                            case "Int32":
                                _value = 0;
                                break;
                            case "Int64":
                                _value = 0;
                                break;
                            case "Decimal":
                                _value = 0;
                                break;
                            case "Double":
                                _value = 0;
                                break;
                            case "Single":
                                _value = 0;
                                break;
                            case "String":
                                _value = "Eb";
                                break;
                            case "Date":
                                _value = DateTime.MinValue;
                                break;
                            case "Datetime":
                                _value = DateTime.MinValue;
                                break;
                            default:
                                _value = 0;
                                break;
                        }
                        globals[TName].Add(fName, new GNTV { Name = fName, Type = (GlobalDbType)(int)typ, Value = _value as object });
                    }
                    if (request.Parameters != null)
                    {
                        foreach (Param p in request.Parameters)
                        {
                            globals["Params"].Add(p.Name, new GNTV { Name = p.Name, Type = (GlobalDbType)Convert.ToInt32(p.Type), Value = p.Value });
                        }
                    }
                    IEnumerable<string> matches2 = Regex.Matches(request.ValueExpression, @"Calc.\w+").OfType<Match>()
                            .Select(m => m.Groups[0].Value)
                            .Distinct();
                    string[] _calcFieldsUsed = new string[matches2.Count()];
                    int j = 0;
                    foreach (string match in matches2)
                        _calcFieldsUsed[j++] = match.Replace("Calc.", string.Empty);
                    foreach (string calcfd in _calcFieldsUsed)
                    {
                        globals["Calc"].Add(calcfd, new GNTV { Name = calcfd, Type = (GlobalDbType)11, Value = 0 });
                    }
                    EbReport R = new EbReport();
                    resultType = R.ExecuteScriptV1(globals, R.CompileScriptV1(request.ValueExpression))?.GetType();

                    //return expression type
                    switch (resultType.FullName)
                    {
                        case "System.Date":
                            resultType_enum = 5;
                            break;
                        case "System.DateTime":
                            resultType_enum = 6;
                            break;
                        case "System.Decimal":
                            resultType_enum = 7;
                            break;
                        case "System.Double":
                            resultType_enum = 8;
                            break;
                        case "System.Int16":
                            resultType_enum = 10;
                            break;
                        case "System.Int32":
                            resultType_enum = 11;
                            break;
                        case "System.Int64":
                            resultType_enum = 12;
                            break;
                        case "System.Single":
                            resultType_enum = 15;
                            break;
                        case "System.String":
                            resultType_enum = 16;
                            break;
                        default:
                            resultType_enum = 0;
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                _isValid = false;
                _excepMsg = e.Message;
                Console.WriteLine(e.Message);
            }
            return new ValidateCalcExpressionResponse { IsValid = _isValid, Type = resultType_enum, ExceptionMessage = _excepMsg };
        }
    }





    //int count = iTextSharp.text.FontFactory.RegisterDirectory("E:\\ExpressBase.Core\\ExpressBase.Objects\\Fonts\\");
    //using (InstalledFontCollection col = new InstalledFontCollection())
    //{
    //    foreach (FontFamily fa in col.Families)
    //    {
    //        Console.WriteLine(fa.Name);
    //    }
    //}

}
