﻿using ExpressBase.Common;
using ExpressBase.Common.Data;
using ExpressBase.Common.ServiceClients;
using ExpressBase.Common.Structures;
using ExpressBase.Objects;
using ExpressBase.Objects.EmailRelated;
using ExpressBase.Objects.Services;
using ExpressBase.Objects.ServiceStack_Artifacts;
using ServiceStack;
using ServiceStack.Messaging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExpressBase.ServiceStack.MQServices
{
    public class PdfToEmailService : EbBaseService
    {
        public PdfToEmailService(IEbConnectionFactory _dbf, IMessageProducer _mqp, IMessageQueueClient _mqc, IEbServerEventClient _sec) : base(_dbf, _mqp, _mqc, _sec) { }

        public void Post(PdfCreateServiceMqRequest request)
        {
            MessageProducer3.Publish(new PdfCreateServiceRequest()
            {
                Refid = request.Refid,
                Params = request.Params,
                UserId = request.UserId,
                UserAuthId = request.UserAuthId,
                SolnId = request.SolnId
            });
        }
    }
    [Restrict(InternalOnly = true)]
    public class PdfToEmailInternalService : EbMqBaseService
    {
        public PdfToEmailInternalService(IMessageProducer _mqp, IMessageQueueClient _mqc) : base(_mqp, _mqc) { }

        public void Post(PdfCreateServiceRequest request)
        {
            EbConnectionFactory ebConnectionFactory = new EbConnectionFactory(request.SolnId, this.Redis);
            var objservice = base.ResolveService<EbObjectService>();
            objservice.EbConnectionFactory = ebConnectionFactory;
            var dataservice = base.ResolveService<DataSourceService>();
            dataservice.EbConnectionFactory = ebConnectionFactory;
            var reportservice = base.ResolveService<ReportService>();
            reportservice.EbConnectionFactory = ebConnectionFactory;
            EbObjectParticularVersionResponse res = (EbObjectParticularVersionResponse)objservice.Get(new EbObjectParticularVersionRequest() { RefId = request.Refid });
            EbEmailTemplate ebEmailTemplate = new EbEmailTemplate();
            foreach (var element in res.Data)
            {
                ebEmailTemplate = EbSerializers.Json_Deserialize(element.Json);
            }
            if (ebEmailTemplate.DataSourceRefId != string.Empty)
            {
                EbObjectParticularVersionResponse myDsres = (EbObjectParticularVersionResponse)objservice.Get(new EbObjectParticularVersionRequest() { RefId = ebEmailTemplate.DataSourceRefId });
                EbDataSource ebDataSource = new EbDataSource();
                ebDataSource = EbSerializers.Json_Deserialize(myDsres.Data[0].Json);
                var parameters = DataHelper.GetParams(ebConnectionFactory, false, request.Params, 0, 0);
                var ds = ebConnectionFactory.ObjectsDB.DoQueries(ebDataSource.Sql, parameters.ToArray());
                var pattern = @"\{{(.*?)\}}";
                IEnumerable<string> matches = Regex.Matches(ebEmailTemplate.Body, pattern).OfType<Match>()
                 .Select(m => m.Groups[0].Value)
                 .Distinct();
                foreach (var _col in matches /*ebEmailTemplate.DsColumnsCollection*/)
                {
                    string str = /*dscol.Title*/_col.Replace("{{", "").Replace("}}", "");

                    foreach (var dt in ds.Tables)
                    {
                        string colname = dt.Rows[0][str.Split('.')[1]].ToString();
                        ebEmailTemplate.Body = ebEmailTemplate.Body.Replace(/*dscol.Title*/_col, colname);
                    }
                }
            }
            var RepRes = reportservice.Get(new ReportRenderRequest { Refid = ebEmailTemplate.AttachmentReportRefID, Fullname = "MQ", Params = request.Params });
            RepRes.StreamWrapper.Memorystream.Position = 0;

            MessageProducer3.Publish(new EmailServicesRequest()
            {
                From = "request.from",
                To = ebEmailTemplate.To,
                Cc = ebEmailTemplate.Cc.Split(","),
                Bcc = ebEmailTemplate.Bcc.Split(","),
                Message = ebEmailTemplate.Body,
                Subject = ebEmailTemplate.Subject,
                UserId = request.UserId,
                UserAuthId = request.UserAuthId,
                SolnId = request.SolnId,
                AttachmentReport = RepRes.ReportBytea,
                AttachmentName = RepRes.ReportName
            });
        }


    }
}
