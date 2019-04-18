using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Reflection;

namespace ICT104ProtocolWrapper
{
    public class BillAcceptEventArgs : EventArgs
    {
        public int amount;

        public BillAcceptEventArgs(int _amount)
        {
            amount = _amount;
        }

    }

    public enum ERR_CODE { None, Motor_Failure = 32, Checksum_Error, Bill_Jam, Bill_Remove, Stacker_Open, Sensor_Problem, Bill_Fish = 39, Stacker_Problem, Bill_Reject, Invalid_Command, Error_Status_Exclusion = 47, IsEnabled = 62, IsInhibited = 94, CannotCheckStatus=999 };

    public enum CRITICAL_ERRORS { Motor_Failure = 32, Checksum_Error, Bill_Jam, Bill_Remove, Stacker_Open, Sensor_Problem, Bill_Fish = 39, Stacker_Problem, Bill_Reject, Invalid_Command, Error_Status_Exclusion = 47 };
    /// <summary>
    /// Based on Document ICT-104 Protocol For RS232
    /// Event-Driven Bill Accepter Class Library
    /// 2018.11 Ryu Youngseok
    /// </summary>
    public class BillAccepter
    {
        public static readonly string Name = "ICT104";
        private SerialPort internalSerialPort = new SerialPort();

        public delegate void ExternalHandler(object sender, BillAcceptEventArgs e);

        /// <summary>
        /// Must chain external event method here.
        /// </summary>
        public event ExternalHandler Received;

        public enum Mode { Idle, Reset, CheckStatus, Accepting };

        public Dictionary<byte, string> CommandNames = new Dictionary<byte, string>();

        private readonly byte sendBillValidated = 0x81;
        private readonly byte billType1 = 0x40;
        private readonly byte billType2 = 0x41;
        private readonly byte billType3 = 0x42;
        private readonly byte billType4 = 0x43;
        private readonly byte billType5 = 0x44;
        private readonly byte accept = 0x02;
        private readonly byte hold = 0x18;
        private readonly byte stacking = 0x10;
        private readonly byte reset = 0x30;
        private readonly byte checkstatus = 0x0C;
        private readonly byte enable = 0x3E;
        private readonly byte disable = 0x5E;
        private readonly byte powerSupplyOn1 = 0x80;
        private readonly byte powerSupplyOn2 = 0x8F;

        private ERR_CODE chkedStatus;

        private byte[] rawData;
        private int billType;
        private bool tempReset = false;
        private bool twiceReset = false;

        private int[] billTypes=null;

        public Mode mode = Mode.Idle;

        public bool IsOpen
        {
            get
            {
                return internalSerialPort.IsOpen;
            }
        }

        public string CurrentPort
        {
            get
            {
                return internalSerialPort.PortName;
            }
        }

        public BillAccepter()
        {
            internalSerialPort.BaudRate = 9600;
            internalSerialPort.StopBits = StopBits.One;
            internalSerialPort.Parity = Parity.Even;
            internalSerialPort.DataBits = 8;
            internalSerialPort.DataReceived += ReceivedData;
            SetCommandDictionary();
            Logger.Info("BillAccepter - Start Up");
        }

        private void SetCommandDictionary()
        {
            BindingFlags bindingFlags = BindingFlags.NonPublic |
                            BindingFlags.Instance |
                            BindingFlags.Static;

            var values = this.GetType()
                     .GetFields(bindingFlags)
                     .Select(field => field.GetValue(this))
                     .ToList();
            var names = typeof(BillAccepter).GetFields()
                            .Select(field => field.Name)
                            .ToList();
            foreach (FieldInfo field in typeof(BillAccepter).GetFields(bindingFlags))
            {
                if (field.IsInitOnly)
                {
                    CommandNames.Add((byte)field.GetValue(this), field.Name.ToUpperInvariant());
                }
            }
        }

        ~BillAccepter()
        {
            if (internalSerialPort.IsOpen)
            {
                Logger.Info("Force Close");
                ClosePort();
            }
            Logger.Info("BillAccepter Disposed");
        }
        /// <summary>
        /// Set the serial port name.
        /// </summary>
        public bool SetPort(string _port)
        {
            try
            {
                internalSerialPort.PortName = _port;
                Logger.Info("BillAccepter - Setted Port " + _port);
                return true;
            }
            catch
            {
                return false;
            }
        }
       
        /// <summary>
        /// Set the 4 cash types. 
        /// </summary>
        /// <param name="_billTypes"></param>
        public void SetBillTypes(int[] _billTypes)
        {
            billTypes = _billTypes;
        }

        /// <summary>
        /// Reset the Accepter.
        /// </summary>
        public void InitializeAccepter()
        {
            if (!internalSerialPort.IsOpen) return;

            ChangeMode(Mode.Reset);
            WriteByte(reset);
        }

        /// <summary>
        /// Open the port, and return bool value depends on success or not.
        /// </summary>
        /// <returns></returns>
        public bool OpenPort()
        {
            try
            {
                internalSerialPort.Open();
                Logger.Info("BillAccepter - Open Port");
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return false;
            }
        }

        /// <summary>
        /// Close the port, and return bool value depends on success or not.
        /// </summary>
        /// <returns></returns>
        public bool ClosePort()
        {
            try
            { 
                internalSerialPort.Close();
                Logger.Info("BillAccepter - Close Port");
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return false;
            }
        }

        /// <summary>
        /// Check the Status of Accepter, and returns it's Error code.
        /// </summary>
        /// <returns></returns>
        public ERR_CODE CheckStatusofAccepter()
        {
            ERR_CODE result;

            if (!internalSerialPort.IsOpen) return ERR_CODE.CannotCheckStatus;

            ChangeMode(Mode.CheckStatus);
            WriteByte(checkstatus);
            int i = 0;
            while (chkedStatus == ERR_CODE.None && i < 10)
            {
                Thread.Sleep(100);
                i++;
            }
            if (i >= 10)
            {
                Logger.Warn("Timeout");
                return CheckStatusofAccepter();
            }

            result = chkedStatus;
            chkedStatus = ERR_CODE.None;
            return result;
        }

        /// <summary>
        /// Open the Accepter.
        /// </summary>
        public void EnableAccepter()
        {
            if (!internalSerialPort.IsOpen) return;
            if (billTypes == null) return;
            WriteByte(enable);
        }

        /// <summary>
        /// Close the Accepter.
        /// </summary>
        public void DisableAccepter()
        {
            if (!internalSerialPort.IsOpen) return;
            WriteByte(disable);
        }

        private void ReceivedData(object sender, SerialDataReceivedEventArgs e)
        {
            if (rawData != null)
            {
                List<byte> currentBuffer = rawData.ToList();
                ByteLog(currentBuffer);
                if (currentBuffer.Count > 0)
                {
                    if (mode != Mode.CheckStatus && System.Enum.IsDefined(typeof(CRITICAL_ERRORS), (int)currentBuffer[0]))
                    {
                        ChangeMode(Mode.Reset);
                        tempReset = true;
                        rawData = null;
                    }

                    if (mode == Mode.Idle)
                    {
                        if (currentBuffer.Contains(sendBillValidated))
                        {
                            if (currentBuffer.Count > 1)
                            {
                                WriteByte(hold);
                                billType = currentBuffer[currentBuffer.IndexOf(sendBillValidated) + 1];
                                WriteByte(accept);
                            }
                            ChangeMode(Mode.Accepting);
                            rawData = null;
                        }
                    }
                    else if (mode == Mode.Accepting)
                    {
                        if (currentBuffer.Contains(stacking))
                        {
                            rawData = null;
                            AcceptBill();
                            ChangeMode(Mode.Idle);
                        }
                        else if (currentBuffer.Contains(billType1) || currentBuffer.Contains(billType2) || currentBuffer.Contains(billType3) || currentBuffer.Contains(billType4) || currentBuffer.Contains(billType5))
                        {
                            WriteByte(hold);
                            billType = currentBuffer[0];
                            rawData = null;
                            WriteByte(accept);
                        }

                    }
                    else if (mode == Mode.Reset)
                    {
                        if (tempReset)
                        {
                            WriteByte(accept);
                            rawData = null;
                            ChangeMode(Mode.Idle);
                            Thread.Sleep(100);
                            EnableAccepter();
                            tempReset = false;
                        }
                        else
                        {
                            if (currentBuffer.Contains(powerSupplyOn1) && currentBuffer.Contains(powerSupplyOn2))
                            {
                                WriteByte(accept);
                                rawData = null;
                                ChangeMode(Mode.Idle);
                                Thread.Sleep(100);
                                DisableAccepter();
                            }
                            else if (currentBuffer.Contains(powerSupplyOn1) || currentBuffer.Contains(powerSupplyOn2))
                            {
                                if (twiceReset == false)
                                {
                                    Logger.Info("Only One PSO signal. wait again");
                                    WriteByte(accept);
                                    rawData = null;
                                    twiceReset = true;
                                }
                                else
                                {
                                    Logger.Info("PSO signal again. Get Twice");
                                    WriteByte(accept);
                                    rawData = null;
                                    ChangeMode(Mode.Idle);
                                    Thread.Sleep(100);
                                    DisableAccepter();
                                    twiceReset = false;
                                }
                            }
                        }
                    }

                    if (mode == Mode.CheckStatus)
                    {
                        chkedStatus = (ERR_CODE)(int)currentBuffer[0];
                        rawData = null;
                        ChangeMode(Mode.Idle);
                    }
                }
                else if (rawData != null)
                {
                    byte[] buffer = new byte[internalSerialPort.BytesToRead];
                    internalSerialPort.Read(buffer, 0, internalSerialPort.BytesToRead);
                    if (buffer != null)
                        rawData.Concat(buffer);
                    ByteLog(currentBuffer);
                }
            }
            else
            {
                byte[] buffer = new byte[internalSerialPort.BytesToRead];
                internalSerialPort.Read(buffer, 0, internalSerialPort.BytesToRead);
                rawData = (byte[])buffer.Clone();
                ReceivedData(sender, e);
            }
        }

        private void AcceptBill()
        {
            int type = -1;

            if (billType == billType1)type = 0;
            else if (billType == billType2)type = 1;
            else if (billType == billType3)type = 2;
            else if (billType == billType4)type = 3;
            else if (billType == billType5) type = 4;
            else type = -1;

            int result;
            try
            {
                result = billTypes[type];
            }
            catch (Exception e)
            {
                Logger.Error(e);
                result = -1;
            }

            Received(this, new BillAcceptEventArgs(result));
        }

        private void WriteByte(byte b)
        {
            internalSerialPort.Write(new byte[] { b }, 0, 1);
            string log = "BillAccepter - Sent: ";
            try
            {
                log += string.Format("{0} ({1}) ", string.Concat("0x", String.Format("{0:X}", b).PadLeft(2, '0')), CommandNames[b]);
            }
            catch
            {
                log += string.Format("{0} ", string.Concat("0x", String.Format("{0:X}", b).PadLeft(2, '0')));
            }
            finally
            {
                Logger.Info(log);
            }
        }

        private void ByteLog(List<byte> buffer)
        {
            string log = "BillAccepter - Received: ";
            foreach (var x in buffer)
            {
                if (CommandNames.ContainsKey(x))
                {
                    log += string.Format("{0} ({1}) ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')), CommandNames[x]);
                }
                else if (System.Enum.IsDefined(typeof(ERR_CODE), (int)x))
                {
                    log += string.Format("{0} ({1}) ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')), (ERR_CODE)(int)x);
                }
                else
                {
                    log += string.Format("{0} ", string.Concat("0x", String.Format("{0:X}", x).PadLeft(2, '0')));
                }
            }
            Logger.Info(log);
        }

        private void ChangeMode(Mode newMode)
        {
            Logger.Info($"Changed Mode from {mode.ToString()} to {newMode.ToString()}");
            mode = newMode;
        }
    }
}
