﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using ExpressBase.Common.Constants;
using ServiceStack;
using ExpressBase.Common;
using ExpressBase.Common.Data;
using ExpressBase.Common.Enums;
using ExpressBase.Objects.ServiceStack_Artifacts;
using ExpressBase.Common.ServiceClients;
using ExpressBase.Common.EbServiceStack.ReqNRes;
using ExpressBase.Common.ServiceStack.ReqNRes;
using System.Linq;
using System.Data.Common;
using ExpressBase.Common.Structures;

namespace ExpressBase.ServiceStack.Services
{
    public class EbFileDownloadService : EbBaseService
    {
        public string UserName { get; set; }
        public string Password { private get; set; }
        public string Host { get; set; }
        public List<KeyValuePair<int, string>> Files { get; set; }

        public EbFileDownloadService(IEbConnectionFactory _dbf, IEbStaticFileClient _sfc)
        : base(_dbf, _sfc)
        {
        }

        //public void ListFilesDirectory(string DirecStructure)
        //{
        //    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(FileDownloadConstants.HostName + DirecStructure + "DICOM/");
        //    request.Timeout = -1;
        //    request.Method = WebRequestMethods.Ftp.ListDirectory;
        //    System.Net.ServicePointManager.Expect100Continue = false;
        //    request.Credentials = new NetworkCredential(UserName, Password);

        //    try
        //    {
        //        FtpWebResponse response = (FtpWebResponse)request.GetResponse();
        //        Stream responseStream = response.GetResponseStream();
        //        StreamReader reader = new StreamReader(responseStream);
        //        string[] files = reader.ReadToEnd().Split('\n');
        //        if (files.Length > 0)
        //        {
        //            for (int i = 0; i < files.Length; i++)
        //            {
        //                if (!files[i].Trim().IsNullOrEmpty())
        //                {
        //                    Console.WriteLine("File Name : " + files[i]);
        //                    Files.Add(files[i]);
        //                }

        //            }
        //        }
        //        Console.WriteLine($"Directory List Complete, status {response.StatusDescription}");
        //        reader.Close();
        //        response.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex);
        //    }
        //}

        //private List<string> ListDirectory(string DirecStructure)
        //{
        //    List<string> DirectoryListing = new List<string>();
        //    FtpWebRequest request = (FtpWebRequest)WebRequest.Create(FileDownloadConstants.HostName + DirecStructure);
        //    request.Timeout = -1;
        //    request.Method = WebRequestMethods.Ftp.ListDirectory;
        //    System.Net.ServicePointManager.Expect100Continue = false;
        //    request.Credentials = new NetworkCredential(UserName, Password);
        //    try
        //    {
        //        FtpWebResponse response = (FtpWebResponse)request.GetResponse();
        //        Stream responseStream = response.GetResponseStream();
        //        StreamReader reader = new StreamReader(responseStream);
        //        string[] Directories = reader.ReadToEnd().Split('\n');
        //        for (int i = 0; i < Directories.Length; i++)
        //        {
        //            //if (!Directories[i].Equals(string.Empty) || !Directories[i].Equals('\r') || !Directories[i].Equals('\n'))
        //            //{
        //            //    DirectoryListing.Add(Directories[i]);
        //            //    Console.WriteLine("Directory Listing: " + Directories[i]);
        //            //}
        //            //if (temp.Equals('\r'))
        //            //    DirectoryListing.Add(temp.Substring(0, temp.Length - 1));
        //            //else
        //            //DirectoryListing.Add(temp);
        //            if (!Directories[i].Equals(string.Empty))
        //            {
        //                if (Directories[i][Directories[i].Length - 1].Equals('\r'))
        //                    DirectoryListing.Add(Directories[i].Substring(0, Directories[i].Length - 1));
        //                else
        //                    DirectoryListing.Add(Directories[i]);
        //            }
        //        }
        //        Console.WriteLine($"Directory List Complete, status {response.StatusDescription}");
        //        reader.Close();
        //        response.Close();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex);
        //    }
        //    return DirectoryListing;
        //}

        private int GetFileNamesFromDb()
        {
            int CustomerId = 0;
            string UploadPath = FileDownloadConstants.HostName + @"files/SoftFiles_L/";
            string ImageTableQuery = @"SELECT customervendor.id, customervendor.accountcode, customervendor.imageid, vddicommentry.filename from vddicommentry
            INNER JOIN customervendor ON customervendor.imagecount > 0 and vddicommentry.patientid=(customervendor.prehead||customervendor.accountcode)";
            string _imageId = string.Empty, _fileName = string.Empty, _accountCode = string.Empty;

            var table = this.EbConnectionFactory.ObjectsDB.DoQuery(ImageTableQuery);
            foreach (EbDataRow row in table.Rows)
            {
                CustomerId = (int)row[0];
                _accountCode = row[1].ToString();
                _imageId = row[2].ToString();
                _fileName = row[3].ToString();
                Files.Add(new KeyValuePair<int, string>(CustomerId, UploadPath + _imageId + "/DICOM" + _fileName));
            }
            return CustomerId;
        }

        //public void DownloadFile(string DirecStructure)//, List<string> fileNames)
        //{
        //    FtpWebRequest request;
        //    FtpWebResponse response;

        //    string path = Host + DirecStructure + "DICOM/";

        //    foreach (string name in Files)
        //    {
        //        if (!name.Equals(string.Empty))
        //        {
        //            request = (FtpWebRequest)WebRequest.Create(path + name);
        //            request.Method = WebRequestMethods.Ftp.DownloadFile;
        //            request.Credentials = new NetworkCredential(UserName, Password);
        //            response = (FtpWebResponse)request.GetResponse();
        //            //WriteFile(response, name);
        //            PushToQueue(response, name);
        //            response.Close();
        //        }
        //    }
        //}

        //[Authenticate]
        //private void Post(FileDownloadRequestObject req)
        //{
        //    string Uname = "ftpUser1";
        //    string pwd = "ftpPassword1";
        //    UserName = Uname;
        //    Password = pwd;
        //    string BasePath = "/files/Softfiles_L/";
        //    List<string> DirStructure = ListDirectory(BasePath);
        //    for (int i = 0; i < 10; i++)
        //    {
        //        string Path = BasePath + DirStructure[i] + "/";
        //        ListFilesDirectory(Path);
        //        DownloadFile(Path);
        //        Files.Clear();
        //    }
        //}

        //private void PushToQueue(FtpWebResponse response, string _fileName)
        //{
        //    Stream responseStream = response.GetResponseStream();
        //    byte[] FileContents = new byte[response.ContentLength];
        //    responseStream.ReadAsync(FileContents, 0, FileContents.Length);
        //    base.MessageProducer3.Publish(new UploadImageRequest()
        //    {
        //        Byte = FileContents,
        //        ImageInfo = new ImageMeta()
        //        {
        //            FileName = _fileName,

        //        }
        //    });
        //}

        private int GetFileRefId()
        {
            string IdFetchQuery = @"INSERT into eb_files_ref(userid, filename) VALUES (1, 'test') RETURNING id";
            var table = this.EbConnectionFactory.ObjectsDB.DoQuery(IdFetchQuery);
            int Id = (int)table.Rows[0][0];
            return Id;
        }

        private int MapFilesWithUser(int CustomerId, int FileRefId)
        {
            int res = 0;
            string MapQuery = @"INSERT into customer_files(customer_id, eb_files_ref_id) values(customer_id=@cust_id, eb_files_ref_id=@ref_id) returning id";
            DbParameter[] MapParams=
            {
                        this.InfraConnectionFactory.DataDB.GetNewParameter("cust_id", EbDbTypes.Int32, CustomerId),
                        this.InfraConnectionFactory.DataDB.GetNewParameter("ref_id", EbDbTypes.Int32, FileRefId)
            };
            var table = this.EbConnectionFactory.ObjectsDB.DoQuery(MapQuery);
            res = (int)table.Rows[0][0];
            return res;
        }

        [Authenticate]
        public void Post(FileDownloadRequestObject req)
        {
            string Uname = "";
            string pwd = "";
            UserName = Uname;
            Password = pwd;
            string FilerefId = string.Empty;
            //string BasePath = "";
            Host = FileDownloadConstants.HostName;
            Files = new List<KeyValuePair<int, string>>();

            int CustomerId = GetFileNamesFromDb();

            //List<string> DirStructure = ListDirectory(BasePath);
            //foreach (var _file in Files)
            //{
            //    string Path = _file;
            //ListFilesDirectory(Path);
            FtpWebRequest request;
            FtpWebResponse response;

            if (Files.Count > 0)
            {
                int count = 0, iter=0;
                foreach (KeyValuePair<int, string> file in Files)
                {
                    iter++;
                    if (!file.Value.Equals(string.Empty))
                    {
                        try
                        {
                            Console.WriteLine(iter.ToString() + ". FileName: " + file.Value);
                            request = (FtpWebRequest)WebRequest.Create(file.Value);//fullpath + name);
                            request.Method = WebRequestMethods.Ftp.DownloadFile;
                            request.Credentials = new NetworkCredential(UserName, Password);
                            response = (FtpWebResponse)request.GetResponse();
                            Stream responseStream = response.GetResponseStream();
                            byte[] FileContents = new byte[response.ContentLength];
                            if (FileContents.Length == 0)
                                throw new Exception("File returned empty");
                            responseStream.ReadAsync(FileContents, 0, FileContents.Length);

                            UploadImageAsyncRequest imgupreq = new UploadImageAsyncRequest();
                            imgupreq.ImageByte = FileContents;
                            imgupreq.ImageInfo = new ImageMeta();
                            imgupreq.ImageInfo.FileCategory = EbFileCategory.Images;
                            imgupreq.ImageInfo.FileName = file.Value;
                            imgupreq.ImageInfo.FileType = file.Value.Split('.').Last();
                            imgupreq.ImageInfo.ImageQuality = ImageQuality.original;
                            imgupreq.ImageInfo.Length = FileContents.Length;
                            imgupreq.ImageInfo.MetaDataDictionary = new Dictionary<string, List<string>>();
                            imgupreq.ImageInfo.FileRefId = GetFileRefId();

                            if (MapFilesWithUser(file.Key, imgupreq.ImageInfo.FileRefId) < 1)
                                throw new Exception("File Mapping Failed");

                            var x = FileClient.Post<UploadAsyncResponse>(imgupreq);
                            response.Close();

                            if (count > 10)
                                break;
                            else
                                count++;
                        }
                        catch(Exception ex)
                        {
                            continue;
                        }
                    }
                }
                Files.Clear();
            }
            else
            {
                Console.WriteLine("There were no files in that directory!\n\n");
            }
        }
    }
}
