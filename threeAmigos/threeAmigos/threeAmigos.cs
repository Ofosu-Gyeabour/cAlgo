using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Media;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class threeAmigos : Robot
    {
        #region User Defined Parameters

        private ExponentialMovingAverage _emaFast;
        private ExponentialMovingAverage _emaSlow;
        private ExponentialMovingAverage _emaMedian;

        private RelativeStrengthIndex _rsi;


        [Parameter("Fast Period", DefaultValue = 15)]
        public int fastPeriod { get; set; }

        [Parameter("Median Period", DefaultValue = 25)]
        public int medianPeriod { get; set; }

        [Parameter("Slow Periods", DefaultValue = 50)]
        public int slowPeriod { get; set; }

        [Parameter("Periods to Consider", DefaultValue = 10)]
        public int periodsToConsider { get; set; }

        [Parameter("Data Source")]
        public DataSeries Price { get; set; }

        [Parameter("Trade Start Time(GMT)", DefaultValue = 4)]
        public int startingTime { get; set; }
        //time from which to enter the market. Default is 04:00GMT
        [Parameter("Trade End Time(GMT)", DefaultValue = 23)]
        public int endingTime { get; set; }
        //threshold time for not entering the market. Default is 21:00GMT

        [Parameter("Opened Position Count", DefaultValue = 4)]
        public int PositionCount { get; set; }

        [Parameter("Lots size", DefaultValue = 1000, MinValue = 0)]
        public int lotSize { get; set; }

        [Parameter("Stop Loss", DefaultValue = 40)]
        public int StopLoss { get; set; }

        [Parameter("Take Profit", DefaultValue = 10)]
        public int TakeProfit { get; set; }


        [Parameter("RSI Period", DefaultValue = 10)]
        public int RSIPeriod { get; set; }

        [Parameter("Elapsed Time", DefaultValue = 140)]
        public int ElapsedTime { get; set; }

        private Dictionary<string, Position> dict = new Dictionary<string, Position>();

        private int vToTrade { get; set; }

        System.Timers.Timer oTimer = null;

        #endregion

        #region cTrader Events

        private void InitializeSystemTimer()
        {
            try
            {
                this.oTimer = new System.Timers.Timer(50000);
                //50 seconds
                this.oTimer.AutoReset = true;
                this.oTimer.Enabled = true;
                this.oTimer.Start();

                //calling the event
                this.oTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.ManageTradePositions2);
            } catch (Exception e)
            {
                Print(e.Message);
            }
        }

        private void ManageTradePositions2(object sender, System.Timers.ElapsedEventArgs e)
        {
            Print("Hello, My name is {0} and I start every {1} seconds", "threeAmigos", "50");
            try
            {
                if (dict.Count > 0)
                {
                    foreach (var dd in dict)
                    {
                        if (dd.Value.Pips > 100)
                        {
                            ClosePosition(dd.Value);
                        }

                        if ((dd.Value.Pips / this.TakeProfit) > 2)
                        {
                            ClosePosition(dd.Value);
                        }

                        if ((dd.Value.TradeType.ToString().ToUpper() == "BUY") && _rsi.Result.IsFalling() && dd.Value.Pips > 0)
                        {
                            ClosePosition(dd.Value);
                        }

                        if ((dd.Value.TradeType.ToString().ToUpper() == "BUY") && _rsi.Result.IsFalling() && dd.Value.Pips <= -30)
                        {
                            ClosePosition(dd.Value);
                        }

                        if ((dd.Value.TradeType.ToString().ToUpper() == "SELL") && _rsi.Result.IsRising() && dd.Value.Pips > 0)
                        {
                            ClosePosition(dd.Value);
                        }

                        if ((dd.Value.TradeType.ToString().ToUpper() == "SELL") && _rsi.Result.IsRising() && dd.Value.Pips <= -30)
                        {
                            ClosePosition(dd.Value);
                        }

                        //dict.Remove(dd.Value.Label);
                    }
                }
            } catch (Exception ee)
            {
                Print(ee.Message);
            }
        }

        protected override void OnStart()
        {
            /* between 9pm and 4am, do NOT enter the market use some of the variables used in the EA for MT4 */

            try
            {
                //this.InitializeSystemTimer();
                //if there are any existing open positions, LOAD them into the dictionary object
                if (dict == null)
                {
                    dict = new Dictionary<string, Position>();
                }

                if (Positions.Count > 0)
                {
                    foreach (var p in Positions)
                    {
                        if (p.Label == string.Empty)
                        {
                            var LAB = generatePositionLabelName(p.SymbolName, p.TradeType.ToString());

                            if (dict.ContainsKey(LAB) == false)
                            {
                                dict.Add(LAB, p);
                            }
                        }
                    }
                }
                else
                {
                    Print("No opened positions existed at the start of the Robot");
                }
            } catch
            {
            }
            try
            {
                Thread childThread = new Thread(VALIDATION_ROUTINES);
                childThread.Start();
            } catch
            {
            }
        }

        protected override void OnBar()
        {

            var longPosition = Positions.Find(string.Empty, SymbolName, TradeType.Buy);
            var shortPosition = Positions.Find(string.Empty, SymbolName, TradeType.Sell);

            try
            {

                if (isValidTradingDay() && isValidTradingTimePeriod() && isValidPositionCount())
                {
                    TradeResult result = null;

                    if (BuyCondition())
                    {
                        result = ExecuteMarketOrder(TradeType.Buy, SymbolName, this.lotSize, generatePositionLabelName(SymbolName, "BUY"), null, null);
                    }
                    else if (SellCondition())
                    {
                        result = ExecuteMarketOrder(TradeType.Sell, SymbolName, this.lotSize, generatePositionLabelName(SymbolName, "SELL"), null, null);
                    }

                    if (result.IsSuccessful)
                    {
                        dict.Add(result.Position.Label, result.Position);
                    }

                    if (dict.Count > 0)
                    {
                        Thread th = new Thread(ManageTradePositions);
                        th.Start();
                    }
                }
            } catch
            {

            }
        }

        private string generatePositionLabelName(string SYMB, string sTRADETYPE)
        {
            // method generates the label name to use as key in a dictionary object 

            var rd = new Random();
            int rdNo = rd.Next(500, 1000);

            return string.Format("{0}{1}{2}", SYMB, sTRADETYPE, rdNo.ToString());
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
            try
            {
                if (dict != null)
                {
                    dict.Clear();
                }
            } catch (Exception e)
            {
                Print(e.Message);
            }
        }

        #endregion

        #region Position Management

        private void ManageTradePositions()
        {
            //close all existing positions having the same tradeType

            try
            {
                foreach (var d in dict)
                {
                    switch (d.Value.TradeType.ToString().ToUpper())
                    {
                        case "BUY":
                            if (_rsi.Result.LastValue > 80 && _rsi.Result.IsFalling())
                            {
                                ClosePosition((Position)d.Value);
                            }


                            if (d.Value.Pips < (double)this.TakeProfit)
                            {
                                TimeSpan t = (DateTime.Now - d.Value.EntryTime);
                                if (t.TotalMinutes >= (double)this.ElapsedTime)
                                {
                                    ClosePosition((Position)d.Value);
                                    try
                                    {
                                        dict.Remove(d.Value.Label);
                                    } catch (Exception e)
                                    {
                                    }
                                }

                            }

                            if (d.Value.Pips > (double)this.TakeProfit)
                            {
                                TimeSpan t = (DateTime.Now - d.Value.EntryTime);
                                if (t.TotalMinutes >= (double)this.ElapsedTime)
                                {
                                    ClosePosition(d.Value);

                                    try
                                    {
                                        dict.Remove(d.Value.Label);
                                    } catch (Exception e)
                                    {
                                    }
                                }

                            }
                            break;
                        case "SELL":
                            if (_rsi.Result.LastValue < 20 && _rsi.Result.IsRising() && (d.Value.Pips > (double)this.TakeProfit))
                            {
                                ClosePosition(d.Value);
                            }


                            if ((d.Value.Pips < (double)this.TakeProfit) && _rsi.Result.IsRising())
                            {
                                TimeSpan t = DateTime.Now - d.Value.EntryTime;
                                if (t.TotalMinutes >= (double)this.ElapsedTime)
                                {
                                    ClosePosition(d.Value);
                                    try
                                    {
                                        dict.Remove(d.Value.Label);
                                    } catch (Exception e)
                                    {
                                    }
                                }
                            }

                            if ((d.Value.Pips / this.TakeProfit >= 1.5) && _rsi.Result.IsRising())
                            {
                                TimeSpan t = (DateTime.Now - d.Value.EntryTime);
                                if (t.TotalMinutes >= (double)this.ElapsedTime)
                                {
                                    ClosePosition(d.Value);
                                    try
                                    {
                                        dict.Remove(d.Value.Label);
                                    } catch (Exception e)
                                    {
                                    }
                                }

                            }
                            break;
                    }

                    //start here

                    if (d.Value.Pips > 100)
                    {
                        ClosePosition(d.Value);
                    }

                    if ((d.Value.Pips / this.TakeProfit) > 2)
                    {
                        ClosePosition(d.Value);
                    }

                    if ((d.Value.TradeType.ToString().ToUpper() == "BUY") && _rsi.Result.IsFalling() && d.Value.Pips > 0)
                    {
                        ClosePosition(d.Value);
                    }

                    if ((d.Value.TradeType.ToString().ToUpper() == "BUY") && _rsi.Result.IsFalling() && d.Value.Pips <= -30)
                    {
                        ClosePosition(d.Value);
                    }

                    if ((d.Value.TradeType.ToString().ToUpper() == "SELL") && _rsi.Result.IsRising() && d.Value.Pips > 0)
                    {
                        ClosePosition(d.Value);
                    }

                    if ((d.Value.TradeType.ToString().ToUpper() == "SELL") && _rsi.Result.IsRising() && d.Value.Pips <= -30)
                    {
                        ClosePosition(d.Value);
                    }


                    //end here

                }

            } catch
            {

            }
        }

        #endregion


        #region Trade Validations

        private void VALIDATION_ROUTINES()
        {
            //validation routines goes here...executed in a separate thread of its own
            if (isValidTradingDay() && isValidTradingTimePeriod())
            {
                _emaFast = Indicators.ExponentialMovingAverage(Price, fastPeriod);
                _emaMedian = Indicators.ExponentialMovingAverage(Price, medianPeriod);
                _emaSlow = Indicators.ExponentialMovingAverage(Price, slowPeriod);

                _rsi = Indicators.RelativeStrengthIndex(Price, RSIPeriod);
            }
        }

        private bool isValidTradingTimePeriod()
        {
            //determines if we are in the valid time for trading
            bool bln = false;
            TimeSpan endTime = new TimeSpan(this.endingTime, 0, 0);
            TimeSpan startTime = new TimeSpan(this.startingTime, 0, 0);
            TimeSpan now = DateTime.Now.TimeOfDay;

            if ((now > startTime) && (now < endTime))
            {
                return (bln = true);
            }
            else
            {
                return bln;
            }
        }

        private bool isValidTradingDay()
        {
            bool bln = false;
            var day = DateTime.Now.DayOfWeek;
            if ((day != DayOfWeek.Saturday) || (day != DayOfWeek.Sunday))
            {
                return (bln = true);
            }
            else
            {
                return bln;
            }
        }

        private bool isValidPositionCount()
        {
            //determines if the number of opened positions is within range
            bool bln = false;
            int pos = Positions.Count;
            if (pos < this.PositionCount)
            {
                return bln = true;
            }
            else
            {
                return bln;
            }

        }

        #endregion

        #region Position Information
        private bool BuyCondition()
        {
            int index = Bars.OpenTimes.Count - 2;
            bool bln = false;

            if ((_emaFast.Result[index] > _emaSlow.Result[index]) && (_rsi.Result.LastValue < 30 && _rsi.Result.IsRising()))
            {
                this.vToTrade = 2000;
                bln = true;
            }

            if ((_emaFast.Result[index] > _emaSlow.Result[index]) && (_rsi.Result.LastValue < 50 && _rsi.Result.IsRising()))
            {
                this.vToTrade = 1000;
                bln = true;
            }
            return bln;
        }

        private bool SellCondition()
        {
            int index = Bars.OpenTimes.Count - 2;
            bool bln = false;

            if ((_emaFast.Result[index] < _emaSlow.Result[index]) && (_rsi.Result.LastValue > 80 && _rsi.Result.IsFalling()))
            {
                this.vToTrade = 2000;
                bln = true;
            }

            if ((_emaFast.Result[index] < _emaSlow.Result[index]) && (_rsi.Result.LastValue > 55 && _rsi.Result.IsFalling()))
            {
                this.vToTrade = 1000;
                bln = true;
            }

            return bln;
        }

        #endregion


    }
}
