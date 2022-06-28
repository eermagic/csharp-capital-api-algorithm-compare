using Future2.Classes;
using SKCOMLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Future2
{
    public partial class FormAlgoSignal : Form
    {
        #region 屬性
        Algorithm algo = null;

        UserProfile user = null;// 使用者物件

        SKCenterLib m_pSKCenter = null;// 登入&環境物件
        SKQuoteLib m_SKQuoteLib = null;// 國內報價物件
        SKReplyLib m_pSKReply = null;// 回應物件

        DataTable dtCapitalQuoteMap = null;

        int nCode = 0;
        double dDigitNum = 0.000; // 小數位
        bool isTesting = false;//測試中

        string filter = "";
        #endregion

        #region 建構子
        public FormAlgoSignal()
        {
            InitializeComponent();
        }

        private void FormAlgoSignal_Load(object sender, EventArgs e)
        {
            // 初始化物件

            // A B 商品比較物件
            algo = new Algorithm();
            dtCapitalQuoteMap = new DataTable();
            dtCapitalQuoteMap.Columns.Add("CommType");
            dtCapitalQuoteMap.Columns.Add("SymbolCode");
            dtCapitalQuoteMap.Columns.Add("SymbolPriceType");
            dtCapitalQuoteMap.Columns.Add("SymbolPrice");
            dtCapitalQuoteMap.Columns.Add("sMarketNo");
            dtCapitalQuoteMap.Columns.Add("nStockIdx");

            // 商品項目
            Dictionary<string, string> mapSymbol = new Dictionary<string, string>();
            mapSymbol.Add("TX00", "大台指期近月");
            mapSymbol.Add("MTX00", "小台指期近月");
            mapSymbol.Add("TE00", "電子期近月");
            mapSymbol.Add("ZE0000", "小型電子期近月");

            // 綁定商品下拉
            foreach (KeyValuePair<string, string> map in mapSymbol)
            {
                cboSymbolA.Items.Add(new ComboboxItem(map.Key, map.Value));
                cboSymbolB.Items.Add(new ComboboxItem(map.Key, map.Value));
            }

            // 價格/委量項目
            Dictionary<string, string> mapPrice = new Dictionary<string, string>();
            mapPrice.Add("PRICE_BID1", "委價_BID1");
            mapPrice.Add("PRICE_BID2", "委價_BID2");
            mapPrice.Add("PRICE_BID3", "委價_BID3");
            mapPrice.Add("PRICE_BID4", "委價_BID4");
            mapPrice.Add("PRICE_BID5", "委價_BID5");
            mapPrice.Add("PRICE_ASK1", "委價_ASK1");
            mapPrice.Add("PRICE_ASK2", "委價_ASK2");
            mapPrice.Add("PRICE_ASK3", "委價_ASK3");
            mapPrice.Add("PRICE_ASK4", "委價_ASK4");
            mapPrice.Add("PRICE_ASK5", "委價_ASK5");


            // 綁定下拉
            cboPriceA.Items.Add(new ComboboxItem("CLOSE", "CLOSE"));
            cboPriceB.Items.Add(new ComboboxItem("CLOSE", "CLOSE"));
            foreach (KeyValuePair<string, string> map in mapPrice)
            {
                cboPriceA.Items.Add(new ComboboxItem(map.Key, map.Value));
                cboPriceB.Items.Add(new ComboboxItem(map.Key, map.Value));
            }

            //比較項目
            Dictionary<string, string> mapLogic = new Dictionary<string, string>();
            mapLogic.Add(">", ">");
            mapLogic.Add("<", "<");
            mapLogic.Add("=", "=");
            foreach (KeyValuePair<string, string> map in mapLogic)
            {
                cboLogicB.Items.Add(new ComboboxItem(map.Key, map.Value));
            }

            // 初始化物件
            m_pSKCenter = new SKCenterLib();
            m_pSKReply = new SKReplyLib();
            m_SKQuoteLib = new SKQuoteLib();

            // 註冊公告事件
            m_pSKReply.OnReplyMessage += new _ISKReplyLibEvents_OnReplyMessageEventHandler(this.m_pSKReply_OnAnnouncement);

            // 國內報價連線狀態事件
            m_SKQuoteLib.OnConnection += new _ISKQuoteLibEvents_OnConnectionEventHandler(m_SKQuoteLib_OnConnection);

            // 國內 Tick 回傳事件
            m_SKQuoteLib.OnNotifyTicksLONG += new _ISKQuoteLibEvents_OnNotifyTicksLONGEventHandler(m_SKQuoteLib_OnNotifyTicks);

            // 國內 Best5 回傳事件
            m_SKQuoteLib.OnNotifyBest5LONG += new _ISKQuoteLibEvents_OnNotifyBest5LONGEventHandler(m_SKQuoteLib_OnNotifyBest5);
        }
        #endregion

        #region 動作
        /// <summary>
        /// 測試訊號
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTestAlgorithm_Click(object sender, EventArgs e)
        {
            // 取出序列化使用者
            string path = ConfigurationManager.AppSettings["UserSavePath"];
            if (File.Exists(path + "\\" + "UserProfile"))
            {
                FileStream fs = new FileStream(path + "\\" + "UserProfile", FileMode.Open);
                IFormatter formatter = new BinaryFormatter();
                user = (UserProfile)formatter.Deserialize(fs);
                fs.Close();
            }
            else
            {
                MessageBox.Show("請先完成帳號設定儲存");
                return;
            }

            if (string.IsNullOrEmpty(cboSymbolA.Text))
            {
                MessageBox.Show("請輸入 [商品(A)]");
                return;
            }
            if (string.IsNullOrEmpty(cboPriceA.Text))
            {
                MessageBox.Show("請輸入 [價格(A)]");
                return;
            }
            if (string.IsNullOrEmpty(cboSymbolB.Text))
            {
                MessageBox.Show("請輸入 [商品(B)]");
                return;
            }
            if (string.IsNullOrEmpty(cboPriceB.Text))
            {
                MessageBox.Show("請輸入 [價格(B)]");
                return;
            }
            if (string.IsNullOrEmpty(cboLogicB.Text))
            {
                MessageBox.Show("請輸入 [比較]");
                return;
            }
            if (string.IsNullOrEmpty(txtParamA.Text))
            {
                MessageBox.Show("請輸入 [參數]");
                return;
            }

            // 寫入演算法比較物件
            algo.SymbolA = ComboUtil.GetItem(cboSymbolA).Value;
            algo.PriceA = ComboUtil.GetItem(cboPriceA).Value;
            algo.SymbolB = ComboUtil.GetItem(cboSymbolB).Value;
            algo.PriceB = ComboUtil.GetItem(cboPriceB).Value;
            algo.LogicB = ComboUtil.GetItem(cboLogicB).Value;
            algo.ParamA = txtParamA.Text;

            // 清空價格表
            dtCapitalQuoteMap.Clear();

            // 不用 SGX DMA
            m_pSKCenter.SKCenterLib_SetAuthority(1);

            // 登入群益帳戶
            nCode = m_pSKCenter.SKCenterLib_Login(user.CapitalUserId, user.CapitalUserPwd);
            if (nCode != 0 && nCode != 2003)
            {
                txtMessage.AppendText(GetMessage("登入", nCode) + "\n");
                return;
            }

            // 檢查連線狀態
            int nConnected = m_SKQuoteLib.SKQuoteLib_IsConnected();

            if (nConnected == 0)
            {
                // 國內報價連線
                nCode = m_SKQuoteLib.SKQuoteLib_EnterMonitorLONG();
                txtMessage.AppendText(GetMessage("國內報價連線", nCode) + "\n");
                txtMessage.ScrollToCaret();
                if (nCode != 0)
                {
                    return;
                }
            }
            else if (nConnected == 1)
            {
                // 連線中
                // 訂閱最新報價
                RequestQuote();

                isTesting = true;
                btnTestAlgorithm.Enabled = false;
                btnStopTest.Enabled = true;
            }
        }
        /// <summary>
        /// 停止測試
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnStopTest_Click(object sender, EventArgs e)
        {
            // 解除訂閱
            CloseRequest();

            btnTestAlgorithm.Enabled = true;
            btnStopTest.Enabled = false;
            isTesting = false;
        }

        /// <summary>
        /// 關閉視窗
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormAlgoSignal_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isTesting)
            {
                // 解除訂閱
                CloseRequest();

                //把整個連線中斷
                nCode = m_SKQuoteLib.SKQuoteLib_LeaveMonitor();

                btnTestAlgorithm.Enabled = true;
                btnStopTest.Enabled = false;
                isTesting = false;
            }
        }
        #endregion

        #region 方法
        /// <summary>
        /// 取得群益api回傳訊息說明
        /// </summary>
        /// <param name="strType"></param>
        /// <param name="nCode"></param>
        /// <param name="strMessage"></param>
        private string GetMessage(string strType, int nCode)
        {
            string strInfo = "";

            if (nCode != 0)
                strInfo = "【" + m_pSKCenter.SKCenterLib_GetLastLogInfo() + "】";

            string message = "【" + strType + "】【" + m_pSKCenter.SKCenterLib_GetReturnCodeMessage(nCode) + "】" + strInfo;
            return message;
        }

        /// <summary>
        /// 取得商品資訊
        /// </summary>
        private void RequestQuote()
        {
            // 訂閱商品A
            DataRow drNew = dtCapitalQuoteMap.NewRow();
            drNew["CommType"] = "A";
            drNew["SymbolCode"] = algo.SymbolA;
            drNew["SymbolPriceType"] = algo.PriceA;
            SKSTOCKLONG pSKStockLONG = new SKSTOCKLONG();
            nCode = m_SKQuoteLib.SKQuoteLib_GetStockByNoLONG(algo.SymbolA, ref pSKStockLONG);
            txtMessage.AppendText(GetMessage("[商品A] 的相關資訊", nCode) + "\n");
            txtMessage.ScrollToCaret();
            if (nCode != 0)
            {
                return;
            }
            drNew["sMarketNo"] = pSKStockLONG.bstrMarketNo;
            drNew["nStockIdx"] = pSKStockLONG.nStockIdx;

            // 更新價格小數位
            dDigitNum = (Math.Pow(10, pSKStockLONG.sDecimal));

            dtCapitalQuoteMap.Rows.Add(drNew);

            // 訂閱商品 B
            drNew = dtCapitalQuoteMap.NewRow();
            drNew["CommType"] = "B";
            drNew["SymbolCode"] = algo.SymbolB;
            drNew["SymbolPriceType"] = algo.PriceB;

            if (algo.SymbolB == algo.SymbolA)
            {
                filter = "CommType = 'A'";
                DataRow dr = dtCapitalQuoteMap.Select(filter)[0];

                drNew["sMarketNo"] = dr["sMarketNo"];
                drNew["nStockIdx"] = dr["nStockIdx"];
            }
            else
            {
                pSKStockLONG = new SKSTOCKLONG();
                nCode = m_SKQuoteLib.SKQuoteLib_GetStockByNoLONG(algo.SymbolB, ref pSKStockLONG);
                txtMessage.AppendText(GetMessage("[商品B] 的相關資訊", nCode) + "\n");
                txtMessage.ScrollToCaret();

                if (nCode != 0)
                {
                    return;
                }

                drNew["sMarketNo"] = pSKStockLONG.bstrMarketNo;
                drNew["nStockIdx"] = pSKStockLONG.nStockIdx;
            }
            dtCapitalQuoteMap.Rows.Add(drNew);

            // 訂閱 Tick & Best5
            RequestTickBest5();
        }

        /// <summary>
        /// 訂閱 Tick & Best5
        /// </summary>
        private void RequestTickBest5()
        {
            short sTickPage;

            // 訂閱商品A
            filter = "CommType = 'A'";
            DataRow[] drsCapitalQuoteMap = dtCapitalQuoteMap.Select(filter);
            sTickPage = Convert.ToInt16(drsCapitalQuoteMap[0]["nStockIdx"]);

            //訂閱 Tick & Best5，訂閱後等待 OnNotifyTicks 及 OnNotifyBest5 事件回報
            nCode = m_SKQuoteLib.SKQuoteLib_RequestTicks(ref sTickPage, algo.SymbolA);
            txtMessage.AppendText(GetMessage("[商品A] 訂閱 Tick & Best5", nCode) + "\n");
            txtMessage.ScrollToCaret();
            if (nCode != 0)
            {
                return;
            }

            // 訂閱商品B
            //訂閱 Tick & Best5，訂閱後等待 OnNotifyTicks 及 OnNotifyBest5 事件回報
            if (algo.SymbolB != algo.SymbolA)
            {
                filter = "CommType = 'B'";
                drsCapitalQuoteMap = dtCapitalQuoteMap.Select(filter);
                sTickPage = Convert.ToInt16(drsCapitalQuoteMap[0]["nStockIdx"]);

                nCode = m_SKQuoteLib.SKQuoteLib_RequestTicks(ref sTickPage, algo.SymbolB);
                txtMessage.AppendText(GetMessage("[商品B] 訂閱 Tick & Best5", nCode) + "\n");
                txtMessage.ScrollToCaret();
                if (nCode != 0)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// 檢查演算法邏輯
        /// </summary>
        private void TestSignal()
        {
            double priceA = 0;
            double priceB = 0;
            double compareA = 0;
            double paramA = 0;
            string Signal = ""; //訊號

            // 取商品a 價格
            filter = "CommType = 'A'";
            DataRow[] drsCapitalQuoteMap = dtCapitalQuoteMap.Select(filter);
            double.TryParse(drsCapitalQuoteMap[0]["SymbolPrice"].ToString(), out priceA);

            //取商品b 價格
            filter = "CommType = 'B'";
            drsCapitalQuoteMap = dtCapitalQuoteMap.Select(filter);
            double.TryParse(drsCapitalQuoteMap[0]["SymbolPrice"].ToString(), out priceB);

            // 取參數a
            paramA = Convert.ToDouble(algo.ParamA);

            if (priceA != 0 && priceB != 0)
            {
                // 計算結果
                compareA = priceA - priceB;
                compareA = Math.Round(compareA, 2);

                // 比較值與參數值相比
                if (algo.LogicB == ">")
                {
                    if (compareA > paramA)
                    {
                        Signal = "商品(A): "+ priceA + " － 商品(B): " + priceB + " 結果: " + compareA + " ＞ " + paramA+ " 時間: " + DateTime.Now.ToString("HH:mm:ss") + "." + DateTime.Now.Millisecond;
                    }
                }
                else if (algo.LogicB == "<")
                {
                    if (compareA < paramA)
                    {
                        Signal = "商品(A): " + priceA + " － 商品(B): " + priceB + " 結果: " + compareA + " ＜ " + paramA + " 時間: " + DateTime.Now.ToString("HH:mm:ss") + "." + DateTime.Now.Millisecond;
                    }
                }
                else if (algo.LogicB == "=")
                {
                    if (compareA == paramA)
                    {
                        Signal = "商品(A): " + priceA + " － 商品(B): " + priceB + " 結果: " + compareA + " ＝ " + paramA + " 時間: " + DateTime.Now.ToString("HH:mm:ss") + "." + DateTime.Now.Millisecond;
                    }
                }


                if (Signal != "")
                {
                    // 輸出畫面訊號
                    if (txtSignal.IsDisposed == false)
                    {
                        txtSignal.AppendText(Signal + "\n");
                        txtSignal.ScrollToCaret();
                    }
                }
            }
        }

        /// <summary>
        /// 解除訂閱商品
        /// </summary>
        private void CloseRequest()
        {
            string symbolList = algo.SymbolA;

            // 解除訂閱商品A Tick
            nCode = m_SKQuoteLib.SKQuoteLib_CancelRequestTicks(algo.SymbolA);
            txtMessage.AppendText(GetMessage("解除訂閱商品A Tick & Best5", nCode) + "\n");
            txtMessage.ScrollToCaret();

            if (algo.SymbolA != algo.SymbolB)
            {
                symbolList = symbolList + "," + algo.SymbolB;

                // 解除訂閱商品B Tick
                nCode = m_SKQuoteLib.SKQuoteLib_CancelRequestTicks(algo.SymbolB);
                txtMessage.AppendText(GetMessage("解除訂閱商品B Tick & Best5", nCode) + "\n");
                txtMessage.ScrollToCaret();
            }

            //取消訂閱SKQuoteLib_RequestStocks的報價通知，並停止更新商品報價。
            nCode = m_SKQuoteLib.SKQuoteLib_CancelRequestStocks(symbolList);
            txtMessage.AppendText(GetMessage("解除訂閱商品即時報價", nCode) + "\n");
            txtMessage.ScrollToCaret();
        }
        #endregion

        #region 事件
        /// <summary>
        /// 公告
        /// </summary>
        void m_pSKReply_OnAnnouncement(string strUserID, string bstrMessage, out short nConfirmCode)
        {
            nConfirmCode = -1;
        }

        /// <summary>
        /// 國內報價連線回應事件
        /// </summary>
        /// <param name="nKind"></param>
        /// <param name="nCode"></param>
        void m_SKQuoteLib_OnConnection(int nKind, int nCode)
        {
            try
            {
                if (nKind == 3001)
                {
                    if (nCode == 0)
                    {
                        // 連線中
                        lblTwSignal.ForeColor = Color.Blue;
                        lblTwSignal.Text = "連線狀態：連線中";
                    }
                }
                else if (nKind == 3002)
                {
                    // 連線中斷
                    lblTwSignal.ForeColor = Color.Red;
                    lblTwSignal.Text = "連線狀態：中斷";

                    btnTestAlgorithm.Enabled = true;
                    btnStopTest.Enabled = false;
                }
                else if (nKind == 3003)
                {
                    // 連線成功
                    lblTwSignal.ForeColor = Color.Green;
                    lblTwSignal.Text = "連線狀態：正常";

                    // 訂閱最新報價
                    RequestQuote();

                    // 畫面狀態
                    isTesting = true;
                    btnTestAlgorithm.Enabled = false;
                    btnStopTest.Enabled = true;
                }
                else if (nKind == 3021)
                {
                    //網路斷線
                    lblTwSignal.ForeColor = Color.DarkRed;
                    lblTwSignal.Text = "連線狀態：網路斷線";
                }
            }
            catch (Exception ex)
            {
                txtMessage.AppendText(ProjectUtil.ErrToStr(ex) + "\n");
                txtMessage.ScrollToCaret();
            }

        }

        /// <summary>
        /// 國內 Tick 回傳事件
        /// </summary>
        void m_SKQuoteLib_OnNotifyTicks(short sMarketNo, int nStockIdx, int nPtr, int nDate, int lTimehms, int lTimemillismicros, int nBid, int nAsk, int nClose, int nQty, int nSimulate)
        {
            bool isUpdate = false;

            filter = "sMarketNo = '" + sMarketNo + "' and nStockIdx = '" + nStockIdx + "' and SymbolPriceType = 'CLOSE'";
            DataRow[] drsCapitalQuoteMap = dtCapitalQuoteMap.Select(filter);

            if (drsCapitalQuoteMap.Length > 0)
            {
                foreach (DataRow dr in drsCapitalQuoteMap)
                {
                    // 價格有所不同才要檢查訊號
                    if ((nClose / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                    {
                        isUpdate = true;
                    }

                    // 更新最新價格
                    dr["SymbolPrice"] = nClose / dDigitNum;
                }
                if (isUpdate)
                {
                    TestSignal();
                }
            }
        }

        /// <summary>
        /// 國內 Best5 回傳事件
        /// </summary>
        void m_SKQuoteLib_OnNotifyBest5(short sMarketNo, int nStockIdx, int nBestBid1, int nBestBidQty1, int nBestBid2, int nBestBidQty2, int nBestBid3, int nBestBidQty3, int nBestBid4, int nBestBidQty4, int nBestBid5, int nBestBidQty5, int nExtendBid, int nExtendBidQty, int nBestAsk1, int nBestAskQty1, int nBestAsk2, int nBestAskQty2, int nBestAsk3, int nBestAskQty3, int nBestAsk4, int nBestAskQty4, int nBestAsk5, int nBestAskQty5, int nExtendAsk, int nExtendAskQty, int nSimulate)
        {
            filter = "sMarketNo = '" + sMarketNo + "' and nStockIdx = '" + nStockIdx + "' and SymbolPriceType <> 'CLOSE'";
            DataRow[] drsCapitalQuoteMap = dtCapitalQuoteMap.Select(filter);
            bool isUpdate = false;

            if (drsCapitalQuoteMap.Length > 0)
            {
                foreach (DataRow dr in drsCapitalQuoteMap)
                {
                    if (dr["SymbolPriceType"].ToString() == "PRICE_BID1")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestBid1 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBid1 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_BID2")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestBid2 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBid2 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_BID3")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestBid3 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBid3 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_BID4")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestBid4 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBid4 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_BID5")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestBid5 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBid5 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_ASK1")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestAsk1 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAsk1 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_ASK2")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestAsk2 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAsk2 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_ASK3")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestAsk3 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAsk3 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_ASK4")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestAsk4 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAsk4 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "PRICE_ASK5")
                    {
                        // 價格有所不同才要檢查訊號
                        if ((nBestAsk5 / dDigitNum).ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAsk5 / dDigitNum;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_BID1")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestBidQty1.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBidQty1;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_BID2")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestBidQty2.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBidQty2;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_BID3")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestBidQty3.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBidQty3;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_BID4")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestBidQty4.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBidQty4;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_BID5")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestBidQty5.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestBidQty5;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_ASK1")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestAskQty1.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAskQty1;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_ASK2")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestAskQty2.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAskQty2;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_ASK3")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestAskQty3.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAskQty3;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_ASK4")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestAskQty4.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAskQty4;
                    }
                    else if (dr["SymbolPriceType"].ToString() == "QTY_ASK5")
                    {
                        // 價格有所不同才要檢查訊號
                        if (nBestAskQty5.ToString() != dr["SymbolPrice"].ToString())
                        {
                            isUpdate = true;
                        }

                        // 更新最新價格
                        dr["SymbolPrice"] = nBestAskQty5;
                    }
                }
                if (isUpdate)
                {
                    // 執行訊號檢查
                    TestSignal();
                }

            }

        }
        #endregion
        
    }
}
