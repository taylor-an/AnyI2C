﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Schema;
using System.Collections;
using System.Reflection;
using NCDEnterprise;
using AnyI2cLib;
using FTD2XX_NET;
using AnyI2C.Data;

namespace AnyI2C
{
    public partial class frmMain : Form
    {
        I2CBridgeX mBridge = null;
        I2CData mData = new I2CData();
        DevicesCollection mDevices;
        I2CBridgeX[] i2cBridgeXs;
        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {

            //InitFtdiControls();
            EnumBridgeX();
            btnScan.Enabled = false;
            btnSend.Enabled = false;
           
            Configure config = new Configure();
            config.Load();
            
            mData = config.Data;
            if (i2cBridgeXs != null)
            {
                for (int i = 0; i < i2cBridgeXs.Length; i++)
                {
                    if (i2cBridgeXs[i].PortName == config.PortName)
                    {
                        mBridge = i2cBridgeXs[i];
                        cmbI2CBridge.SelectedIndex = i;
                        if (config.Opened)
                        {
                            OpenI2CAdapter();
                        }
                        break;
                    }
                }
            }
            if (mData == null)
            {
                mData = new I2CData();
            }
        
            ctlI2CAddress1.Addr7 = mData.Address;
            UpdateGUIFromData(mData.Content);
            chkWrite.Checked = mData.IsWrite;
            chkRead.Checked = mData.IsRead;
            numReadLength.Value = mData.ReadDataLength;
            SetFormat(mData.Format);
            cmbLogDataType.SelectedIndex = (int)config.LogDataType;
            LoadDevicesConfigure();
        }


        private void UpdateGUIFromData(byte[] content)
        {
            txtQuickSend.Text = "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < content.Length; i++)
            {
                if (mData.Format == emViewFormat.Dec)
                {
                    sb.Append(string.Format("{0} ", content[i]));
                }
                else
                {
                    sb.Append(string.Format("{0:X2} ", content[i]));
                }
            }
            txtQuickSend.Text = sb.ToString();
        }


        private void EnumBridgeX()
        {
            try
            {
                i2cBridgeXs = I2CBridgeX.EnumBridge();
                cmbI2CBridge.Items.Clear();
                for (int i = 0; i < i2cBridgeXs.Length; i++)
                {
                    cmbI2CBridge.Items.Add("I2C Bridge - " + i2cBridgeXs[i].PortName);
                }
                if (i2cBridgeXs.Length > 0)
                {
                    cmbI2CBridge.SelectedIndex = 0;
                }
            }
            catch
            {
            }

        }


        /// <summary>
        /// get view format from format 
        /// </summary>
        /// <returns></returns>
        private emViewFormat GetFormat()
        {
            if (cmbShowFormat.Text == "HEX")
            {
                return emViewFormat.Hex;
            }
            return emViewFormat.Dec;
        }


        private void SetFormat(emViewFormat format)
        {
            if (format == emViewFormat.Hex)
            {
                cmbShowFormat.SelectedIndex = 0;
            }
            else
            {
                cmbShowFormat.SelectedIndex = 1;
            }

        }

        private void cmbI2CBridge_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index == -1)
            {
                string str = "None ";
                e.Graphics.DrawString(str, e.Font, System.Drawing.Brushes.Black, e.Bounds);
                e.DrawFocusRectangle();
            }
            else
            {
                string str = "I2C Bridge - " + i2cBridgeXs[e.Index].PortName;
                e.Graphics.DrawString(str, e.Font, System.Drawing.Brushes.Black, e.Bounds);
                e.DrawFocusRectangle();
            }
        }

        private void OpenI2CAdapter()
        {
            int id = cmbI2CBridge.SelectedIndex;
            //if (id != -1)
            {
               // mBridge = i2cBridgeXs[id];
                mBridge = new I2CBridgeX();
                mBridge.OpenSetting();
                mBridge.Open();
                //mBridge.Open();
                mBridge.OnReadData += OnReadDataHandler;
                mBridge.OnWriteData += OnSendDataHandler; 
                if (mBridge.IsOpen)
                {
                    lbStatus.Text = "Selected I2C Bridge Opened.";
                    btnSend.Enabled = true;
                    btnScan.Enabled = true;
                }
                else
                {
                    btnSend.Enabled = false;
                    btnScan.Enabled = false;
                    lbStatus.Text = "Fail to open selected I2C bridge.";
                }
                lbConnection.Text = mBridge.GetDescription();
                Debug.Print(mBridge.IsOpen.ToString());
            }

        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenI2CAdapter();
        }

        private void cmbI2CBridge_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (mBridge != null)
            {
                if (mBridge.IsOpen)
                {
                    mBridge.Close();
                    OpenI2CAdapter();
                }
            }
            
        }


        private byte[] GetInstanceData()
        {
            emViewFormat format = mData.Format;
            string str = txtQuickSend.Text;
            byte[] data = new byte[0];

            switch (format)
            {
                case emViewFormat.Dec:
                    data = CommonData.GetBytesFromDecString(str);
                    break;
                case emViewFormat.Hex:
                    data = CommonData.GetBytesFromHexString(str);
                    break;
                default:
                    break;
            }
            return data;
        }

        private void txtQuickSend_TextChanged(object sender, EventArgs e)
        {
            HelpFunction.ValidateResult result = HelpFunction.CheckInput(txtQuickSend.Text, mData.Format);
            if (result.IsValid)
            {
                lbInfo.IsShowText = false;
                lbInfo.Value = new int[] { GetInstanceData().Length };
                lbInfo.ForeColor = Color.Black;
            }
            else
            {
                lbInfo.IsShowText = true;
                lbInfo.Text = "Wrong Data";
                lbInfo.ForeColor = Color.Red;
            }
        }



        public  byte [] Send()
        {
            try
            {
                UpdateData();
                mData.Address = ctlI2CAddress1.Addr7;
                if (chkWrite.Checked)
                {

                    bool b = mBridge.Write2((byte)numPort.Value, ctlI2CAddress1.Addr7, mData.Content);
                    
                    if(!b)
                    {
                        LogText("Write Data Fail");
                    }
                }


                if (chkRead.Checked && numReadLength.Value > 0)
                {
                    byte[] readData = mBridge.ReadData2((byte)numPort.Value, ctlI2CAddress1.Addr7, (byte)numReadLength.Value);

                    if (IsFail(readData))
                    {
                        LogText("Read Data Fail");
                    }else if (readData != null && cmbLogDataType.SelectedIndex == 0)
                    {
                        StringBuilder sb = new StringBuilder();
                        string format = GetFormat() == emViewFormat.Hex ? "{0:X2} " : "{0:d} ";
                        sb.Append("R: ");
                        for (int i = 0; i < readData.Length; i++)
                        {
                            sb.AppendFormat(format, readData[i]);
                        }

                        LogText(sb.ToString());
                        return readData;
                    }
                }


            }
            catch 
            {
            }
            return null;
        }

        public byte[] Send(I2CData data)
        {
            mData = data;
            UpdateGUIFromData(mData.Content);
            ctlI2CAddress1.Addr7 = mData.Address;
            chkRead.Checked = mData.IsRead;
            chkWrite.Checked = mData.IsWrite;
            numReadLength.Value = mData.ReadDataLength;
            return Send();
        }


        /// <summary>
        /// check if hte byte array is fail code
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        bool IsFail(byte[] b)
        {
            if(b != null)
            {
                if(b.Length == 4)
                {
                    if(b[0] == 0xBC && b[1] == 0x5C && b[2] == 0xA3 && b[3] ==0x43)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            Send();
        }

        public void LogText(string text)
        {
            txtLog.AppendText(text + Environment.NewLine);
        }

        public int GetCurrentPort()
        {
            return Convert.ToInt32(numPort.Value);
        }

        /// <summary>
        /// if the input is valid
        /// </summary>
        private bool ValidContent
        {
            get
            {
                bool valid = false;
                try
                {
                    if (txtQuickSend.Text != string.Empty)
                    {
                        if (mData.Format == emViewFormat.Dec)
                        {
                            byte[] data1 = CommonData.GetBytesFromDecString(txtQuickSend.Text);
                        }
                        else
                        {
                            byte[] data1 = CommonData.GetBytesFromHexString(txtQuickSend.Text);
                        }
                        valid = true;
                    }
                    else
                    {
                        valid = true;
                    }


                }
                catch
                {
                    valid = false;
                }
                return valid;
            }
        }

        private void cmbShowFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!ValidContent)
            {
                cmbShowFormat.SelectedIndexChanged -= cmbShowFormat_SelectedIndexChanged;
                SetFormat(mData.Format);
                cmbShowFormat.SelectedIndexChanged += cmbShowFormat_SelectedIndexChanged;
                return;
            }
            UpdateData();
            mData.Format = GetFormat();
            UpdateGUIFromData(mData.Content);
        }


        private void UpdateData()
        {
            mData.Content = GetDataFromGUI();
            mData.IsWrite = chkWrite.Checked;
            mData.IsRead = chkRead.Checked;
            mData.ReadDataLength = (byte)numReadLength.Value;
        }

        /// <summary>
        /// Create data from gui
        /// </summary>
        /// <returns></returns>
        private byte[] GetDataFromGUI()
        {
            byte addr = (byte)(ctlI2CAddress1.Addr7 * 2);
            byte[] data2 = new byte[1] { addr };
            byte[] data = CommonData.GetBytes(txtQuickSend.Text, mData.Format, "");
            if (data != null)
            {
                data2 = new byte[data.Length + 1];
                data2[0] = addr;
                for (int i = 0; i < data.Length; i++)
                {
                    data2[i + 1] = data[i];
                }
            }
            //return data2;
            return data;
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            UpdateData();
            mData.Address = ctlI2CAddress1.Addr7;
            Controls.frmI2CDataEdit frm = new Controls.frmI2CDataEdit(mData, this);
            if (frm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                mData = frm.Data;
                UpdateGUIFromData(mData.Content);
                ctlI2CAddress1.Addr7 = mData.Address;
                chkRead.Checked = mData.IsRead;
                chkWrite.Checked = mData.IsWrite;
                numReadLength.Value = mData.ReadDataLength;

            }
        }

        private void btnScan_Click(object sender, EventArgs e)
        {
            frmScanI2C frm = new frmScanI2C(mBridge, (byte)numPort.Value );
            frm.SelectedAddr = ctlI2CAddress1.Addr7;
            if (frm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (frm.SelectedAddr != -1)
                {
                    ctlI2CAddress1.Addr7 = (byte)frm.SelectedAddr;
                }
            }
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            UpdateData();
            try
            {
                Configure config = new Configure();
                config.LogDataType = (enumLogDataType) cmbLogDataType.SelectedIndex;
                config.PortName = mBridge.PortName;
                config.Data = mData;
                config.Opened = mBridge.IsOpen;
                config.Save();
            }
            catch
            {
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            string path = System.IO.Directory.GetParent(Application.ExecutablePath) + "\\ADS7828.dll";
            Assembly guiLib = Assembly.LoadFile(path);
            //Type t = guiLib.GetType("I2CDIO8.MyGUI");
            Type t = guiLib.GetType("ADS7828.MyGUI");
            GuiInterface gui = (GuiInterface)Activator.CreateInstance(t);
            CommObj obj = new CommObj(this);
            
            gui.Show(obj);

        }

        /// <summary>
        /// load devices configure
        /// </summary>
        private void LoadDevicesConfigure()
        {
            mDevices = new DevicesCollection();
            listViewDevices.Items.Clear();
            
            mDevices.LoadAllDevices();
            foreach (DeviceConfig dev in mDevices.Devices)
            {
                ListViewItem item = new ListViewItem();
                item.Text = dev.Type;
                item.SubItems.Add(dev.Name);
                listViewDevices.Items.Add(item);
            }
            lbDevices.Text = string.Format("Devices ({0})", listViewDevices.Items.Count);
            if (mDevices.Devices.Length > 0)
            {
                FillCommandsTree(mDevices.Devices[0]);
                FillProperties(mDevices.Devices[0]);
            }
        }


        /// <summary>
        /// Fill Commands tree with 
        /// </summary>
        private void FillCommandsTree(DeviceConfig dev)
        {

            tvCommands.Nodes.Clear();
            DeviceCommandsGroup cmds = dev.Commands;
            if (cmds.Commands != null)
            {
                foreach (DeviceCommandBase cmd in cmds.Commands)
                {
                    TreeNode node = FillCommandsTreeSubNode(cmd);
                    tvCommands.Nodes.Add(node);
                }
            }
            ctlI2CAddress1.Addr7 = dev.Address;
        }

        /// <summary>
        /// fill device's properties
        /// </summary>
        /// <param name="dev"></param>
        private void FillProperties(DeviceConfig dev)
        {
            lbManufactory.Text = string.Format("Manufactory: {0} ", dev.Manufactory);
            lbType.Text = string.Format("Device Type: {0} ", dev.Type);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < dev.AddressCollection.Length; i++)
            {
                sb.AppendFormat("{0} (0x{0:X2})  ", dev.AddressCollection[i]);
            }
            lbAddress.Text = string.Format("Address Collection: {0} ", sb.ToString());
            lbGeneralCall.Text = dev.GeneralCall ? "Support General Call" : "Do NOT support General Call";
            lbGeneralCall.ForeColor = dev.GeneralCall ? Color.Black : Color.Red;
        }

        /// <summary>
        /// Fill command tree 
        /// </summary>
        /// <param name="node"></param>
        /// <param name="cmd"></param>
        private TreeNode FillCommandsTreeSubNode( DeviceCommandBase cmd)
        {
            TreeNode nd = new TreeNode();
            nd.Tag = cmd;
            if (cmd is DeviceCommandsGroup)
            {
                DeviceCommandsGroup g = (DeviceCommandsGroup)cmd;
                nd.Text = g.ToString();
                if (g.Commands != null)
                {
                    foreach (DeviceCommandBase c in g.Commands)
                    {
                        nd.Nodes.Add(FillCommandsTreeSubNode(c));
                    }
                }
            }
            else if (cmd is DeviceCommand)
            {
                nd.Text = cmd.ToString();
            }
            else if (cmd is DeviceGUICommand)
            {
                nd.Text = cmd.ToString();
            }
            return nd;
        }

        private void tvCommands_DoubleClick(object sender, EventArgs e)
        {
            TreeNode nd = tvCommands.SelectedNode;
            if (nd != null)
            {
                if (nd.Tag is DeviceCommand)
                {
                    DeviceCommand cmd = (DeviceCommand)nd.Tag;
                    txtQuickSend.Text = cmd.GetSendDataString(GetFormat() == emViewFormat.Hex);
                    chkWrite.Checked = cmd.Write;
                    chkRead.Checked = cmd.ReadDataLength > 0;
                    numReadLength.Value = cmd.ReadDataLength;
                    Send();
                }
                else if (nd.Tag is DeviceGUICommand)
                {
                    string path = "";
                    try
                    {
                        DeviceGUICommand cmd = (DeviceGUICommand)nd.Tag;
                        path = System.IO.Directory.GetParent(Application.ExecutablePath) + "\\" + cmd.GUIPath;
                        Assembly guiLib = Assembly.LoadFile(path);
                        //Type t = guiLib.GetType("I2CDIO8.MyGUI");
                        Type t = guiLib.GetType(cmd.TypeName);
                        GuiInterface gui = (GuiInterface)Activator.CreateInstance(t);
                        CommObj obj = new CommObj(this);

                        gui.Show(obj);
                    }
                    catch 
                    {
                        MessageBox.Show("Fail to load GUI " + path);
                    }
                }
            }
        }

        private void tvCommands_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode nd = tvCommands.SelectedNode;
            if (nd != null)
            {
                if (nd.Tag is DeviceCommand)
                {
                    DeviceCommand cmd = (DeviceCommand)nd.Tag;
                    lbCommandDes.Text = cmd.Description;
                }
                else if (nd.Tag is DeviceGUICommand)
                {
                    DeviceGUICommand cmd = (DeviceGUICommand)nd.Tag;
                    lbCommandDes.Text = cmd.Description + "\r\n" + cmd.GUIPath;
                }
            }

        }

        private void OnWriteDataError()
        {
            LogText("Write Data Error");
        }

        private void OnReadDataError()
        {
            LogText("Read Data Error");
        }

        /// <summary>
        /// test if it is read only package
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool IsReadOnly(byte[] data)
        {
            if (data.Length == 1)
            {
                if ((data[0] & 0x1) == 1)    // read bit set
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Send i2c data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="readLength"></param>
        /// <returns></returns>
        public byte[] SendI2C(byte[] writedata, byte readLength)
        {
            try
            {
                I2CData data = new I2CData();
                byte[] data2 = new byte[writedata.Length - 1];
                for (int i = 1; i < writedata.Length; i++)
                {
                    data2[i - 1] = writedata[i];
                }

                // check if it is read only package
                if (IsReadOnly(writedata) && readLength > 0)
                {
                    data.IsWrite = false;
                }
                else
                {
                    data.IsWrite = true;
                }

                data.Content = data2;
                data.ReadDataLength = readLength;
                data.IsRead = readLength > 0;
                data.Address = (byte)(writedata[0]);
                data.Format = GetFormat();
                mData = data;
                UpdateGUIFromData(mData.Content);
                ctlI2CAddress1.Addr7 = mData.Address;
                chkRead.Checked = mData.IsRead;
                chkWrite.Checked = mData.IsWrite;
                numReadLength.Value = mData.ReadDataLength;

                return Send();
            }
            catch (Exception ex)
            {
                LogText(ex.Message);
            }
            return null;

        }

        public byte GetCurrentAddress()
        {
            return ctlI2CAddress1.Addr7;
        }

        private void btnTest_Click(object sender, EventArgs e)
        {
            //I2CBridgeX [] bridgeX = I2CBridgeX.EnumBridge();
            //mBridge.Write((byte)numPort.Value, (byte)ctlI2CAddress1.Addr7, 10, 12);
            //mBridge.Write((byte)numPort.Value, (byte)ctlI2CAddress1.Addr7, 10);
            //byte[] data = mBridge.ReadData((byte)numPort.Value, (byte)ctlI2CAddress1.Addr7, 1);
            //FillCommandsTree(mDevices.Devices[0]);
            //FillProperties(mDevices.Devices[0]);
            mDevices.Devices[0].Save("test.xml");
        }

        public void OnReadDataHandler(object sender, ReadDataEventArgs e  )
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("R: ");
            byte[] data = e.Data;
            if (cmbLogDataType.SelectedIndex == 0)
            {
                return; // show i2c received data in "send" route
                //data = GetRecI2CData(e.Data);
            }
            else if (cmbLogDataType.SelectedIndex == 1)  // command data
            {
                data = GetRecCommandData(e.Data);
            }
            else if (cmbLogDataType.SelectedIndex == 2) // api data
            {
                data = e.Data;
            }
            if (data != null)
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (cmbShowFormat.SelectedIndex == 0)   //hex
                    {
                        sb.Append(string.Format("{0:X2} ", data[i]));
                    }
                    else
                    {
                        sb.Append(string.Format("{0} ", data[i]));
                    }
                }

            }
            if (data != null)
            {
                LogText(sb.ToString());

            }
        }

        public void OnSendDataHandler(object sender, WriteDataEventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("W: ");
            byte []data = null;
            if(cmbLogDataType.SelectedIndex == 0)
            {
                data = GetSendI2cData(e.Data);
            }
            else if (cmbLogDataType.SelectedIndex == 1)  // command data
            {
                data = GetSendCommandData(e.Data);
            }
            else if (cmbLogDataType.SelectedIndex == 2) // api data
            {
                data = e.Data;
            }
            if (data != null )
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (cmbShowFormat.SelectedIndex == 0)   //hex
                    {
                        sb.Append(string.Format("{0:X2} ", data[i]));
                    }
                    else
                    {
                        sb.Append(string.Format("{0} ", data[i]));
                    }
                }
                LogText(sb.ToString());

            }

        }

        public byte[] GetSendI2cData(byte[] apiData)
        {
            if (apiData == null)
            {
                return null;
            }
            if (apiData.Length < 4)
            {
                return null;
            }
            byte[] data = null;
            if (apiData[2] == 191) // i2c read
            {
                data = new byte[1] { apiData[3] }; // address
            }
            else if(apiData [2] == 190) // i2c write
            {
                data = new byte[apiData.Length - 4];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = apiData[i + 3];
                }
            }
            return data;
        }


        public byte[] GetSendCommandData(byte [] apiData)
        {
            if (apiData == null)
            {
                return null;
            }
            if (apiData.Length < 4)
            {
                return null;
            }
            byte[] data = new byte[apiData.Length - 3];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = apiData[i + 2];
            }
            return data;
        }


        public byte [] GetRecI2CData(byte[] apiData)
        {
            byte[] data = null;
            if (apiData != null)
            {
                if(apiData.Length >= 4)
                {
                    data = new byte[apiData.Length - 3];
                    for(int i = 0; i < data.Length; i++)
                    {
                        data[i] = apiData[2 + i];
                    }
                }
            }
            return data;
        }

        public byte[] GetRecCommandData(byte[] apiData)
        {
            byte[] data = GetRecI2CData(apiData);
            return data;
        }

        private void cmbLogDataType_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }

        public void SetPort(byte port)
        {
            numPort.Value = port;
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            // Apply the filter to list
        }

        private void listViewDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(listViewDevices.SelectedIndices.Count > 0)
            {
                int id = listViewDevices.SelectedIndices[0];
                if (id != -1)
                {
                    DeviceConfig dev = (DeviceConfig)(mDevices.Devices[id]);
                    FillCommandsTree(dev);
                    FillProperties(dev);
                }
            }
        }

        private void numPort_ValueChanged(object sender, EventArgs e)
        {
            if(mBridge.IsOpen)
            {

            }
        }
    }

    public enum enumLogDataType
    {
        I2C,
        RAW
    }

    public class Configure
    {
        public uint LocationID = 0; // location id of the i2cbridge device
        public bool Opened = false; // if the bridge is opened
        public string PortName = string.Empty;
        public I2CData Data;        // current i2c data

        public enumLogDataType LogDataType = enumLogDataType.I2C;

        //load the configure file
        public void Load()
        {
            try
            {
                XmlReader reader = XmlReader.Create("Config.xml");
                XmlSerializer serializer = new XmlSerializer(typeof(Configure));
                Configure temp = (Configure)serializer.Deserialize(reader);
                Data = temp.Data;
                LocationID = temp.LocationID;
                Opened = temp.Opened;
                PortName = temp.PortName;
                LogDataType = temp.LogDataType;

                reader.Close();
            }
            catch (Exception e)
            {
                Debug.Print("Fail to load configure file.");
            }
        }

        // save the configure file
        public void Save()
        {

            try
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.IndentChars = "\t";
                settings.OmitXmlDeclaration = true;     //omit declaration
                settings.NewLineOnAttributes = true;
                XmlWriter writer = XmlWriter.Create("Config.xml", settings);
                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                ns.Add(String.Empty, String.Empty);
                XmlSerializer serializer = new XmlSerializer(typeof(Configure));
                serializer.Serialize(writer, this, ns); 
                //writer.Flush();
                writer.Close();
            }
            catch (Exception e)
            {
                Debug.Print("Fail to save configure file.");
            }
        }

    }



    public class CommObj : CommInterface
    {
        /// <summary>
        /// main form object, it is a shortcut for plug to access the main form for everything
        /// </summary>
        public frmMain mForm;

        public CommObj(frmMain frm  )
        {
            mForm = frm;
        }

        /// <summary>
        /// Send data through main form
        /// </summary>
        /// <param name="writedata"></param>
        /// <param name="readLength"></param>
        /// <returns></returns>
        public byte[]  Send(byte[] writedata, byte readLength)
        {
            return mForm.SendI2C(writedata, readLength);
        }

        /// <summary>
        /// Get the current address of main form
        /// </summary>
        /// <returns></returns>
        public byte GetDefaultAddress()
        {
            if (mForm != null)
            {
                return mForm.GetCurrentAddress();
            }
            return 0;
        }

        /// <summary>
        /// Get the current port of main form
        /// </summary>
        /// <returns></returns>
        public int GetPort()
        {
            if (mForm != null)
            {
                return mForm.GetCurrentPort();
            }
            return 0;
        }

        /// <summary>
        /// Set the port of main form frm plug
        /// </summary>
        /// <param name="port"></param>
        public void SetPort(byte port)
        {
            if (mForm != null)
            {
                mForm.SetPort(port);
            }
        }

        public void LogText(string text)
        {
            if (mForm != null)
            {
                mForm.LogText(text);
            }
        }


        // Get the last Receiving data
        public byte[] GetLastReceivingData()
        {
            return null; // not implement yet
        }

        // Get the last Sending data
        public byte[] GetLastTransmitData()
        {
            return null; // not implement yet
        }
    }


}
