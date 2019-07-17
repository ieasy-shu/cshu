using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Data;
using UFSoft.UBF.PL;
using UFSoft.UBF.Business;
using UFSoft.UBF.Sys.Database;
using UFSoft.UBF.Util.DataAccess;
using UFIDA.UBF.MD.Business;
using UFIDA.U9.MO.Enums;
using UFIDA.U9.Base;
using UFSoft.UBF.Util.Log;
using UFIDA.U9.PM.PO;
using UFIDA.U9.SM.SMBE;
using UFIDA.U9.CBO.Pub;
using UFIDA.U9.Base.Contact;
using System.IO;
using System.Net;
using UFIDA.U9.Base.UserRole;
using UFSoft.UBF.Analysis.MD.Report.Service;
using UFSoft.UBF.PRN.Control;
using UFSoft.UBF.Report.Tools;
using UFSoft.UBF.Analysis.MD.Report.Service;
using UFSoft.UBF.Analysis.Interface.MD.Report.Service;
using UFSoft.UBF.Analysis.Interface.MD.Report.Model;
using UFIDA.U9.Base.FlexField.ValueSet;


namespace YY.U9.Cust.CALB.AppPlugIn
{

    /// <summary>
    /// 采购订单
    /// </summary>
    [UFSoft.UBF.Eventing.Configuration.Failfast]
    class POSubscriber : UFSoft.UBF.Eventing.IEventSubscriber
    {
       
        public string poid = "";
        public string dyfilename = "";
        public string dyfileurl = "";
        public string khname = "";
        public string scdocno = "";
        public string fjname = "";
        public string fjurl = "";
        private static readonly ILogger logger = LoggerManager.GetLogger(typeof(POSubscriber));

        /// <summary>
        /// 
        /// </summary>
        /// <param name="localFile"></param>
        /// <returns></returns>
        private string UploadFile(string localFile)
        {
            FileInfo fi = new FileInfo(localFile);
            FileStream fs = fi.OpenRead();
            long length = fs.Length;
            fjname = khname + scdocno + ".pdf";

            FtpWebRequest req = (FtpWebRequest)WebRequest.Create("ftp://" + "10.20.1.54" + "/" + fjname);
            req.Credentials = new NetworkCredential("OA-admin", "Oa123456");

            req.Method = WebRequestMethods.Ftp.UploadFile;
            req.UseBinary = true;
            req.ContentLength = length;
            req.Timeout = 10 * 1000;
            try
            {
                Stream stream = req.GetRequestStream();

                int BufferLength = 2048; //2K   
                byte[] b = new byte[BufferLength];
                int i;
                while ((i = fs.Read(b, 0, BufferLength)) > 0)
                {
                    stream.Write(b, 0, i);
                }
                stream.Close();
                stream.Dispose();


            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            dyfilename = fi.Name;
        }

        public static string GetDefineValueNameByCode(string valueSetDefCode, string code)
        {
            string defineValueName = string.Empty;
            DefineValue dv = DefineValue.Finder.Find("ValueSetDef.Code=@ValueSetDefCode and Code=@Code", new OqlParam[] { new OqlParam(valueSetDefCode), new OqlParam(code) });
            if (dv != null)
            {
                defineValueName = dv.Name;
            }
            return defineValueName;
        }


        public void Notify(params object[] args)
        {
            #region 从事件参数中取得当前业务实体

            //从事件参数中取得当前业务实体
            if (args == null || args.Length == 0 || !(args[0] is UFSoft.UBF.Business.EntityEvent))
                return;
            BusinessEntity.EntityKey key = ((UFSoft.UBF.Business.EntityEvent)args[0]).EntityKey;
            if (key == null)
            {
                return;
            }

            Contact cc = new Contact();
            PurchaseOrder po = key.GetEntity() as PurchaseOrder;
            if (po == null)
            {
                return;
            }
            poid = po.ID.ToString();
            
            

            #region OA接口

            string oaProfileValue = Common.GetProfileValue(Common.iProfileCode, Context.LoginOrg.ID);
            if (oaProfileValue.ToLower() != "true")
            {
                return;
            }
            string docTypeCode = po.DocumentType.Code.ToString();
            if (docTypeCode == "PO31" || docTypeCode == "PO32" || docTypeCode == "PO33" || docTypeCode == "PO34" || docTypeCode == "PO35" || docTypeCode == "PO36")
            {

                if (po.OriginalData.Status == PODOCStatusEnum.Approving && po.Status == PODOCStatusEnum.Approving)
                {
                    logger.Error("不能修改：:");
                    if (po.DescFlexField.PrivateDescSeg29 == "")
                    {
                        if (Context.LoginUser.ToString() != "administrator" && Context.LoginUser.ToString() != "admin" && Context.LoginUser.ToString() != "系统管理员")
                        {
                            throw new Exception("OA流程单据不允许手动修改！");
                        }
                    }

                }

                if (po.OriginalData.Status == PODOCStatusEnum.Approving && po.Status == PODOCStatusEnum.Approved)
                {
                    logger.Error("不能审核：:");
                    if (po.DescFlexField.PrivateDescSeg29 == "")
                    {
                        if (Context.LoginUser.ToString() != "administrator" && Context.LoginUser.ToString() != "admin" && Context.LoginUser.ToString() != "系统管理员")
                        {
                            throw new Exception("OA流程单据：" + po.DocNo + "不允许手工审核！");
                        }
                    }
                }
            }
            

            string svURL = Common.GetProfileValue(Common.iProfileCode, Context.LoginOrg.ID);

            string orgCode = Context.LoginOrg.Code;
            string orgName = Context.LoginOrg.Name;
            string docNo = po.DocNo;
            string operatorsName = po.PurOper != null ? po.PurOper.Name : string.Empty;
            string title = operatorsName + docNo;
            decimal totalMoney = 0;
            string docTypeName = po.DocumentType.Name.ToString();
            
            if (po.ID != 0) { }
            Int64 ID = po.ID;
            string OperatorsCode = po.PurOper != null ? po.PurOper.Code.ToString() : string.Empty;

            string BusinessDate = po.BusinessDate.ToString("yyyy-MM-dd");
            string Settlement = po.DescFlexField.PrivateDescSeg7.ToString();
            //decimal TotalMoney = po.TotalMnyAC;
            string ProjectUse = po.DescFlexField.PubDescSeg26.ToString();
            string TradeLocation = po.DescFlexField.PrivateDescSeg5.ToString();
            string Supplier = po.Supplier != null ? po.Supplier.Supplier.Name : string.Empty;
            string supplierContact = po.Supplier.Supplier.DescFlexField.PrivateDescSeg3;
            string supplierPhone = po.Supplier.Supplier.DescFlexField.PrivateDescSeg4;
            string supplierEmail = po.Supplier.Supplier.DescFlexField.PrivateDescSeg11;//正式库和测试库不一样
            khname = po.Supplier != null ? po.Supplier.Supplier.Name : string.Empty;
            scdocno = po.DocNo;
            //string supplierPhone = "";
            //if (cc.DefaultPhoneNum != null) { supplierPhone = cc.DefaultPhoneNum; }
            //string supplierContact = "";
            //if (supplierPhone != null) { supplierContact = cc.PersonName.DisplayName +  supplierPhone; }
            //组织编码	OrgCode
            //组织名称	OrgName
            //Title	标题	bt
            //DocTypeName	单据类型	djlx
            //ID	单据ID	djid
            //DocNo	单号	cgdh
            //OperatorsCode	申请人编码	zdrbm
            //OperatorsName	申请人	zdrmc
            //BusinessDate	日期	cjsj
            //Settlement	结算方式及期限	jsfs
            //TotalMoney	合同总金额(小写）	htzjexx
            //    合同总金额（大写）	
            //ProjectUse	项目用途	xmyt
            //TradeLocation	交货地点	jhdd
            //Supplier	供应商	gys
            //SupplierContact	供应商联系人+供应商电话	gyslxr

            Hashtable ht = new Hashtable();
            ht.Add("zzbm", orgCode);//组织编码
            ht.Add("zzmc", orgName);//组织名称
            ht.Add("cgdh", docNo);
            ht.Add("zdrmc", operatorsName);
            ht.Add("bt", title);
            ht.Add("djlx", docTypeName);
            ht.Add("djlxbm", docTypeCode);
            ht.Add("djid", ID);
            ht.Add("zdrbm", OperatorsCode);
            ht.Add("cjsj", BusinessDate);
            ht.Add("jsfs", Settlement);

            ht.Add("xmyt", ProjectUse);
            ht.Add("jhdd", TradeLocation);
            ht.Add("gys", Supplier);
            if (Supplier == "中航锂电科技有限公司" || Supplier == "中航锂电（洛阳）有限公司" || Supplier == "中航锂电技术研究院有限公司")
            {
                ht.Add("nbqsdw", Supplier);
            }
            ht.Add("gyslxr", supplierContact + supplierPhone);
            
            
            ht.Add("gysyx", supplierEmail);
            
            List<Hashtable> htDetailList = new List<Hashtable>();
            StringBuilder sqlBuilder = new StringBuilder();
            foreach (POLine line in po.POLines)
            {
                //行号	DocLineNo
                //料号	ItemCode
                //品名	ItemName
                //数量	PurQtyTU
                //单位	UomName
                //单价	FinallyPriceTC
                //金额	TotalMnyTC
                //交货时间	DeliveryDate
                Int64 id = line.ID;
                int docLineNo = line.DocLineNo;
                //料号
                string itemCode = line.ItemInfo.ItemCode;
                //品名
                string itemName = line.ItemInfo.ItemName;
                string uomName = line.TradeUOM.Name;
                string deliveryDate = line.POShiplines[0].DeliveryDate.ToString("yyyy-MM-dd");//交期
                decimal purQtyTU = line.SupplierConfirmQtyPU;
                decimal finallyPriceTC = line.FinallyPriceTC;
                decimal totalMnyTC = line.TotalMnyTC;
                totalMoney += totalMnyTC;


                //sqlBuilder.Append("INSERT INTO Cust_PO()");
                //sqlBuilder.Append("VALUES(''); ");
                //lh
                //pm
                //sl
                //dw
                //dj
                //je
                //jhrq

                Hashtable htDetail = new Hashtable();
                htDetail.Add("xid", id);
                htDetail.Add("xh", docLineNo);
                htDetail.Add("lh", itemCode);//料号
                htDetail.Add("pm", itemName);//品名
                htDetail.Add("dw", uomName);
                htDetail.Add("jhrq", deliveryDate);
                htDetail.Add("sl", purQtyTU);
                htDetail.Add("dj", finallyPriceTC);
                htDetail.Add("je", totalMnyTC);
                htDetailList.Add(htDetail);
            }
            ht.Add("htzjexx", totalMoney);
            //if (sqlBuilder.Length > 0)
            //{
            //    //DAOHelper.ExecuteSql(sqlBuilder.ToString());
            //    //DataAccessor.RunSQL(DatabaseManager.GetConnection(), sqlBuilder.ToString(), null);
            //}


            string userid = Context.LoginUserID;
            User uscc = User.Finder.FindByID(userid);
            //string uid = cc.UuID;
            int userID = 0;
            if (po.OriginalData.Status == PODOCStatusEnum.Opened && po.Status == PODOCStatusEnum.Approving)
            {
                string fileurl = "";
                #region 获取当前单据的附件信息
                    //通过单据找到对应的附件
                    string entityFullName = po.GetType().FullName;
                    string opath = "EntityFullName='" + entityFullName + "' and EntityID=" + po.ID;
                    UFIDA.U9.Base.Attachment.Attachment.EntityList attachmentList = UFIDA.U9.Base.Attachment.Attachment.Finder.FindAll(opath, null);
                    if (attachmentList != null && attachmentList.Count > 0)
                    {
                        logger.Error("准备执行上传附件操作……");
                        
                        foreach (UFIDA.U9.Base.Attachment.Attachment attachment in attachmentList)
                        {
                            //附件ID
                            long attachmentID = attachment.AttachmentID;
                            //根据附件ID查文件信息
                            //UFIDA.U9.CS.Collaboration.Library.FileInfo fileInfo = UFIDA.U9.CS.Collaboration.Library.FileInfo.Finder.FindByID(attachmentID);

                            //文件内容存储ID
                            string fileHandler = attachment.FileHandler;

                            //根据文件内容存储ID找文件信息
                            UFIDA.U9.CS.Common.FileStorage.Storage storage = new UFIDA.U9.CS.Common.FileStorage.Storage();
                            //获取文件
                            UFIDA.U9.CS.Common.FileDBService.FileInfo file = storage.GetFile(fileHandler);

                            //获取文件流
                            Stream stream = storage.GetFileContent(fileHandler);
                            stream.Position = 0;
                            //文件流转换成字节
                            byte[] infbytes = new Byte[(int)stream.Length];
                            stream.Read(infbytes, 0, infbytes.Length);
                            stream.Seek(0, SeekOrigin.Begin);

                            //UploadFile("d:/0304.txt");
                            
                            string strRet = Convert.ToBase64String(infbytes);
                            //string localpath = @"D:\0304.txt";
                            //string userName = "OA-admin";
                            //string password="Oa123456";
                            ////C:\Users\Administrator\Desktop
                            ////logger.Error("上传文件失败，失败原因；" + fileInfo.FullPath);
                            //System.Net.NetworkCredential Credentials = new System.Net.NetworkCredential(userName, password);
                            //FtpHelper.UploadFileToFTPServer(localpath, "ftp://10.20.1.54", Credentials);
                            //<a href= "ftp://nc:Jinhe2018@192.168.10.7:21/fdc/fdc_pr/FDC000000000JRA9JU2A/1231.txt\"  target=\"_blank\">1231.txt</ a>
                            string filen = "";
                            
                            fileurl += "/" + file.FileName + ",";
                            
                            //fileurl += "<a href='" + ftpurl + "" + filen + "' target=\"_blank\">" + filen + "</a>";
                            FtpUploader fileload = new FtpUploader();
                            fileload.UploadString(strRet, "ftp://10.20.1.54/", file.FileName, stream);
                            stream.Dispose();
                        }


                    }
                

                #endregion

                #region OA创建流程

                //域账号   DomainName
                //userID = Convert.ToInt32(uscc.UuID);
                userID = Convert.ToInt32(uscc.ShortName);
                string requestName = "采购订单" + docNo + "审批";
                string workflowId = po.DocType.DescFlexField.PrivateDescSeg1;//237测试 
                if (string.IsNullOrEmpty(workflowId))
                {
                    return;
                }
                logger.Error("准备打印模板上传……");
                if (docTypeCode == "PO31" || docTypeCode == "PO32" || docTypeCode == "PO33" || docTypeCode == "PO34" || docTypeCode == "PO36")
                {
                    ShipUpdown();
                    UploadFile(dyfileurl);
                    fjurl = "/" + fjname + ",";
                }
                else if (docTypeCode == "PO35")
                {
                    fjurl = "";
                }
                
                logger.Error("附件文件名1：:" + fileurl);
                fileurl = fileurl.Replace(" filename=", "");
                
                logger.Error("附件文件名2：:" + fjurl);
                ht.Add("fj", fjurl);
                ht.Add("zccllj", fileurl);
                fjurl = "";
                fileurl = "";
                khname ="";
                scdocno = "";
                string workflowRequestInfo = Common.CreateXmlString(requestName, userID, workflowId, ht, htDetailList);
                logger.Error("发送采购单Xml:" + workflowRequestInfo);
                logger.Error("OA用户ID：:" + userID);

                logger.Error("测试采购订单:" + po.OriginalData.Status + "---" + PODOCStatusEnum.Opened + "---" + po.Status + "---" + PODOCStatusEnum.Approved);
                if (docTypeCode == "PO31" || docTypeCode == "PO32" || docTypeCode == "PO33" || docTypeCode == "PO34" || docTypeCode == "PO35" || docTypeCode == "PO36")
                {

                    //调用OA服务
                    WebReference.WorkflowServiceXml client = new WebReference.WorkflowServiceXml();
                    client.Url = Common.GetProfileValue(Common.sProfileCode, Context.LoginOrg.ID);//无效URL
                    client.Url = client.Url.Substring(0, client.Url.Length - 5);
                    string rtnMsg = string.Empty;
                    logger.Error("测试采购订单:" + po.OriginalData.Status + "---" + PODOCStatusEnum.Opened + "---" + po.Status + "---" + PODOCStatusEnum.Approved);
                    try
                    {
                        rtnMsg = client.doCreateWorkflowRequest(workflowRequestInfo, userID);
                        //po.DescFlexField.PrivateDescSeg19 = rtnMsg;
                        using (ISession session = Session.Open())
                        {
                            string sql = "update PM_PurchaseOrder   set DescFlexField_PrivateDescSeg19='"+rtnMsg+"' where id=" + po.ID;
                            DataAccessor.RunSQL(DataAccessor.GetConn(), sql, null);
                            session.Commit();
                        }
                        logger.Error("测试Xml:" + rtnMsg);
                        
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("调用OA服务出现错误：" + ex.Message);
                    }
                }

            }

            //请购单提交状态点击保存 OA执行更新操作
            //else if (pr.OriginalData.Status == PRStatusEnum.Approving && pr.Status == PRStatusEnum.Approving)
            //{
            //    rtnMsg = client.submitWorkflowRequest(workflowRequestInfo, int.Parse(oaRquestID), userID, "submit", "");
            //}
            //if (rtnMsg.StartsWith("Error", StringComparison.OrdinalIgnoreCase))
            //{
            //    logger.Error("请购单OA系统错误:" + rtnMsg);
            //    throw new Exception("【请购单】OA系统错误—>" + rtnMsg);
            //}
            //pr.DescFlexField.PrivateDescSeg29 = "True";

            #endregion


            #endregion
        }



        public string ShipUpdown()
        {
            string printTemplateID = "";
            string OrgID="";
            PurchaseOrder po = PurchaseOrder.Finder.FindByID(poid);
            OrgID = Context.LoginOrg.Code.ToString();
            if (OrgID == "1001")
            {
                if (po.DocumentType.Code.ToString() == "PO31") {
                    printTemplateID = "7a794236-de24-48ca-9d56-be1c7d4ec122";
                    //printTemplateID = "dbecd59b-5a87-45c4-89bc-c112a19ca389";
                }
                else if (po.DocumentType.Code.ToString() == "PO32") {
                    printTemplateID = "dbecd59b-5a87-45c4-89bc-c112a19ca389";
                    
                }
                else if (po.DocumentType.Code.ToString() == "PO33" || po.DocumentType.Code.ToString() == "PO34")
                {
                    if (po.TC.Name == "人民币元")
                    {
                        printTemplateID = "c8edd94d-960f-44f3-904e-d2be529d01ce";
                    }
                    else
                    {
                        printTemplateID = "8067b04f-8f2f-4d1b-a098-db82b9a43012";
                    }
                }
                else if (po.DocumentType.Code.ToString() == "PO36")
                {
                    printTemplateID = "7a794236-de24-48ca-9d56-be1c7d4ec122";
                    //printTemplateID = "dbecd59b-5a87-45c4-89bc-c112a19ca389";
                }
            }
            else if (OrgID == "1002")
            {
                if (po.DocumentType.Code.ToString() == "PO31")
                {
                    printTemplateID = "a303c5ee-ad16-4d31-96c5-0c4dfbab88a8";
                   
                }
                else if (po.DocumentType.Code.ToString() == "PO32")
                {
                    printTemplateID = "f12ff6c7-79c3-4e8e-80a4-6338400c3395";
                }
                else if (po.DocumentType.Code.ToString() == "PO33" || po.DocumentType.Code.ToString() == "PO34")
                {
                    if (po.TC.Name == "人民币元")
                    {
                        printTemplateID = "0768ce62-a296-4d78-bc4a-5c7d98b39d2c";
                    }
                    else
                    {
                        printTemplateID = "cb2408ee-fe67-4762-82cc-4e1f54403c36";
                    }
                }
                else if (po.DocumentType.Code.ToString() == "PO36")
                {
                    printTemplateID = "a303c5ee-ad16-4d31-96c5-0c4dfbab88a8";

                }
            }


            string connectionString = UFSoft.UAP.Report.Base.DBConnectionHelper.GetConnectionString();
            string cultrueName = UFSoft.UBF.Util.Context.PlatformContext.Current.Culture;
            ReportMDService service = ReportMDService.GetInstance(connectionString, cultrueName);
            IReportMDReader reader = service.GetReader();
            IReportTemplate reportTemplate = reader.GetReportTemplateByID(printTemplateID);
            UFSoft.UBF.Report.Entity.Report report = (UFSoft.UBF.Report.Entity.Report)reportTemplate.Content;
            reader.Dispose();

            DataSet ds = GetPrintData(poid);
            string fileName = OutPutFile(report, ds);

            logger.Error("文件路径：" + fileName);
            dyfileurl = fileName;
            return fileName;
        }
        private string OutPutFile(UFSoft.UBF.Report.Entity.Report template, DataSet data)
        {
            PrintService printService = new PrintService();
            PrintCreater printCreater = new PrintCreater();
            printService.LoadXmlFormatFromString(printCreater.CreateReport(template).InnerXml);
            printService.LoadPrintData(data);
            printService.ConvertStart();
            string[] resultFilesName = printService.GetResultFilesName();
            return printService.PhysicalFilePrefix + resultFilesName[0];
        }

        private DataSet GetPrintData(string id)
        {
            DataSet ds = new DataSet();

            DataTable dt = new DataTable();

            dt.Columns.Add("PurchaseOrder_Supplier_Name");
            dt.Columns.Add("PurchaseOrder_DocNo");
            dt.Columns.Add("PurchaseOrder_TC_Name");
            dt.Columns.Add("签订时间");
            dt.Columns.Add("PurchaseOrder_POLines_DocLineNo");
            dt.Columns.Add("料号");
            dt.Columns.Add("品名及规格");
            dt.Columns.Add("PurchaseOrder_POLines_PurQtyPU");
            dt.Columns.Add("PurchaseOrder_POLines_PriceUOM_Name");
            dt.Columns.Add("PurchaseOrder_POLines_FinallyPriceTC");
            dt.Columns.Add("行税率");
            dt.Columns.Add("PurchaseOrder_POLines_TotalMnyTC");
            dt.Columns.Add("要求交货日期");
            dt.Columns.Add("备注");
            dt.Columns.Add("交货地点");
            dt.Columns.Add("运输方式");
            dt.Columns.Add("质保期");
            dt.Columns.Add("供应商档案结算方式及期限");
            dt.Columns.Add("结算方式及期限");
            dt.Columns.Add("税组合");
            dt.Columns.Add("合同有效期");
            dt.Columns.Add("供应商地址扩展段");
            dt.Columns.Add("供应商联系人扩展段");
            dt.Columns.Add("供应商电话扩展段");
            dt.Columns.Add("供应商传真扩展段");
            dt.Columns.Add("供应商开户行扩展段");
            dt.Columns.Add("供应商账号扩展段");
            //Ship ship = Ship.FindByID(id);
            PurchaseOrder po = PurchaseOrder.Finder.FindByID(poid);
            string oql = "select * from UFIDA::U9::PM::PO::POMemo where PurchaseOrder='" + id + "'";
            UFIDA.U9.PM.PO.POMemo memobz = POMemo.Finder.Find("PurchaseOrder=" + po.ID);
            UFIDA.U9.PM.PO.POMemo.EntityList pomemo = POMemo.Finder.FindAll("PurchaseOrder=" + po.ID);
            string bzvalue="";
            if(pomemo.Count>0){
                bzvalue=memobz.Description.ToString();
            }
            //po.POMemos


            foreach (POLine line in po.POLines)
            {
                DataRow newRow = dt.NewRow();
                newRow["PurchaseOrder_Supplier_Name"] = po.Supplier != null ? po.Supplier.Supplier.Name : string.Empty;
                newRow["PurchaseOrder_DocNo"] = po.DocNo;
                newRow["PurchaseOrder_TC_Name"] = po.FC != null ? po.FC.Name.ToString() : string.Empty;
                newRow["签订时间"] = po.DescFlexField.PrivateDescSeg3.ToString();
                newRow["PurchaseOrder_POLines_DocLineNo"] = line.DocLineNo;
                newRow["料号"] = line.ItemInfo.ItemCode;
                newRow["品名及规格"] = line.ItemInfo.ItemName;
                newRow["PurchaseOrder_POLines_PurQtyPU"] = line.SupplierConfirmQtyPU;
                newRow["PurchaseOrder_POLines_PriceUOM_Name"] = line.TradeUOM.Name;
                newRow["PurchaseOrder_POLines_FinallyPriceTC"] = line.FinallyPriceTC;
                newRow["行税率"] = line.TaxRate;
                newRow["PurchaseOrder_POLines_TotalMnyTC"] = line.TotalMnyTC;
                newRow["要求交货日期"] = line.POShiplines[0].DeliveryDate.ToString("yyyy-MM-dd");
                newRow["备注"] = bzvalue;
                newRow["交货地点"] = po.DescFlexField.PrivateDescSeg5.ToString();
                newRow["运输方式"] = GetDefineValueNameByCode("PO_06", po.DescFlexField.PrivateDescSeg6.ToString());
                newRow["质保期"] = po.DescFlexField.PrivateDescSeg12.ToString();
                newRow["供应商档案结算方式及期限"] = po.Supplier.Supplier.DescFlexField.PrivateDescSeg10;
                newRow["结算方式及期限"] = po.DescFlexField.PrivateDescSeg7.ToString();
                newRow["税组合"] = line.TaxSchedule.Name.ToString();
                newRow["合同有效期"] = po.DescFlexField.PrivateDescSeg21.ToString();
                newRow["供应商地址扩展段"] = po.Supplier.Supplier.DescFlexField.PrivateDescSeg2;
                newRow["供应商联系人扩展段"] = po.Supplier.Supplier.DescFlexField.PrivateDescSeg3;
                newRow["供应商电话扩展段"] = po.Supplier.Supplier.DescFlexField.PrivateDescSeg4;
                newRow["供应商传真扩展段"] = po.Supplier.Supplier.DescFlexField.PrivateDescSeg5;
                newRow["供应商开户行扩展段"] = po.Supplier.Supplier.DescFlexField.PrivateDescSeg6;
                newRow["供应商账号扩展段"] = po.Supplier.Supplier.DescFlexField.PrivateDescSeg7;
                dt.Rows.Add(newRow);
            }
            /*
             * foreach(ShipLine in ship。ShipLines)
             * {
                 *  DataRow newRow = dt.NewRow();
                    newRow["Ship_OrderBy_Name"] = "";
                    dt.Rows.Add(newRow);
             * }
             * 
             * 
            List<string> docLineList = new List<string>();
            StringBuilder oqlBuilder = new StringBuilder();
            oqlBuilder.Append("select Ship.OrderBy.Name as Ship_OrderBy_Name,WhMan.Name as Ship_ShipLines_WhMan_Name,ItemInfo.ItemName as Ship_ShipLines_ItemInfo_ItemName,InvUom.Name as Ship_ShipLines_InvUom_Name,WH.Name as Ship_ShipLines_WH_Name,ShipToSite.Name as Ship_ShipLines_ShipToSite_Name,ShipQtyTUAmount as Ship_ShipLines_ShipQtyTUAmount,Ship.ShipMode as Ship_ShipMode,DocLineNo as Ship_ShipLines_DocLineNo,Ship.DocNo as Ship_DocNo,Ship.BusinessDate as Ship_BusinessDate,InvUom.Round.Precision as Ship_ShipLines_InvUom_Round_Precision,Ship.OrderBy.Customer.CustomerContacts.Contact.Name as Ship_OrderBy_Customer_CustomerContacts_Contact_Name,Ship.OrderBy.Customer.CustomerContacts.Contact.Phones.PhoneNum as Ship_OrderBy_Customer_CustomerContacts_Contact_Phones_PhoneNum,Ship.OrderBy.Customer.CustomerContacts.Contact.Addresses.Location.Address1 as Ship_OrderBy_Customer_CustomerContacts_Contact_Addresses_Location_Address1,PriceUom.Round.Precision as Ship_ShipLines_PriceUom_Round_Precision,Ship.OrderBy.Customer.OfficialLocation.Address1 as Ship_OrderBy_Customer_OfficialLocation_Address1,' ' as ShipQtyTUAmount,' ' as ShipQtyTBUAmount,TradeBaseUom.Round.Precision as Ship_ShipLines_TradeBaseUom_Round_Precision,SONo as Ship_ShipLines_SONo,SrcDocNo as Ship_ShipLines_SrcDocNo,Ship.Seller.Name as Ship_Seller_Name,ItemInfo.ItemCode as Ship_ShipLines_ItemInfo_ItemCode,MaturityDate as Ship_ShipLines_MaturityDate,ShipLineMemo as Ship_ShipLines_ShipLineMemo,'' as ConfigureMount,Ship.Org.Name as Ship_Org_Name,'' as OperatorName,'' as PrintDate,'' as FinalPrice,Ship.ID as Ship_ID,ID as Ship_ShipLines_ID  ");
            oqlBuilder.Append(" from UFIDA::U9::SM::Ship::ShipLine");
            oqlBuilder.Append(" where Ship=").Append(id);

            UFSoft.UBF.Business.EntityViewQuery query = new UFSoft.UBF.Business.EntityViewQuery();
            ds = query.ExecuteDataSet(query.CreateQuery(oqlBuilder.ToString()), null);
            */
            ds.Tables.Add(dt);

            return ds;
        }

    }
}
#endregion