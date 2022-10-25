/*
** Developer: Nana Ofosu Gyeabour Appiah
   The Strategy is the second version of PurposeScalper. It works with positional management
   It will open less trades and take BIG SWINGS
   Date: 20th June, 2021
*/
using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class PurposeScalper : Robot
    {

        private RelativeStrengthIndex rsi { get; set; }

        private bool tDirection;

        [Parameter("TakeProfit", DefaultValue = 1.2)]
        public double TakeProfit { get; set; }

        [Parameter("Data Source")]
        public DataSeries Price { get; set; }

        [Parameter("RSI Period", DefaultValue = 5)]
        public int RSIPeriod { get; set; }

        [Parameter("Positions", DefaultValue = 4)]
        public int PositionCount { get; set; }

        [Parameter("PipSizeAttribute", DefaultValue = 20)]
        public int PipSizeAttribute { get; set; }

        [Parameter("EvaluationPeriod", DefaultValue = 15000)]
        public int EvaluationPeriod { get; set; }

//drawdown percentage value
        [Parameter("DRAWDOWN (%)", DefaultValue = 10.0)]
        public double DRAWDOWN { get; set; }

        [Parameter("LTCount", DefaultValue = 10)]
        public int LTCount { get; set; }

//the size of the profit to aim for
        [Parameter("Chunk", DefaultValue = 0.25)]
        public double Chunk { get; set; }

        System.Timers.Timer oTimer = null;
        public double rsiData;

        Dictionary<double, string> entries = new Dictionary<double, string>();

        public bool TREND_FLAG;
        public double RSI_VALUE;

//determines the value at which to enter a buy or sell position in the future
        public double LAST_BUY;
        public double NEXT_BUY;
        public long NEXT_BUY_VOLUME;

        public double LAST_SELL;
        public double NEXT_SELL;
        public long NEXT_SELL_VOLUME;

        public bool tradeBuy { get; set; }
        public bool tradeSell { get; set; }


        public TradeType tradeToManage { get; set; }

        public double TradeBalance { get; set; }
        public double ddnCounter { get; set; }

        public int LOSE_COUNT { get; set; }
        //public int WIN_COUNT { get; set; }

        private Thread th;
        //beginning trade balance

        #region Initialize Variables

        protected override void OnStart()
        {
            try
            {
                LAST_BUY = 0.0;
                NEXT_BUY = 0.0;

                LAST_SELL = 0.0;
                NEXT_SELL = 0.0;

                //initialize the volumes to start trading with

                NEXT_BUY_VOLUME = 50000;
                NEXT_SELL_VOLUME = 50000;

                //long _vol = Symbol.QuantityToVolumeInUnits(Symbol.LotSize);

                this.tradeBuy = true;
                this.tradeSell = true;

                LOSE_COUNT = 0;

                this.InitializeSystemTimer();
                this.rsi = Indicators.RelativeStrengthIndex(Price, RSIPeriod);
                this.TradeBalance = Account.Balance;

            } catch (Exception e)
            {
                Print(e.Message);
            }
        }

        private void InitializeSystemTimer()
        {
            try
            {
                this.oTimer = new System.Timers.Timer(this.EvaluationPeriod);
                this.oTimer.AutoReset = true;
                this.oTimer.Enabled = true;
                this.oTimer.Start();

                this.oTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.Executor);


            } catch (Exception e)
            {
                Print(e.Message);
            }
        }

        private void Executor(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                //increment losing countdown counter if a losing trade exists             
                this.LOSE_COUNT += 1;

                //manage positions using a new thread
                th = new Thread(() => ManageTradePositions("*"));
                th.Priority = ThreadPriority.Highest;
                th.Start();

            } catch (Exception x)
            {
                Print(x.Message);
            }
        }


        #endregion

        #region System Events

        protected override void OnTick()
        {
            try
            {
                TREND_FLAG = rsi.Result.IsRising() ? true : false;
                RSI_VALUE = Math.Round(rsi.Result.LastValue, 2);

                ChartObjects.DrawText("RSI DATA|TREND", RSI_VALUE.ToString() + " | " + (rsi.Result.IsRising() ? "RISING" : "FALLING"), StaticPosition.TopRight, Colors.Yellow);
                //create another chart signaling drawdown level
                this.ddnCounter = Math.Round((((Account.Equity - this.TradeBalance) / this.TradeBalance) * 100), 2);
                ChartObjects.DrawText("DRAWDOWN", "DRAWDOWN: " + this.ddnCounter.ToString(), StaticPosition.TopCenter, Colors.Yellow);

                this.TradeValidators();

            } catch (Exception ee)
            {
                Print(ee.Message + " from tick()");
            }
        }

        protected override void OnBar()
        {
            //On every bar, evaluate trading conditions
            //not doing anything now
            try
            {
                //this.TradeValidators();
            } catch (Exception ee)
            {
                Print(ee.Message + " from onBar()");
            }
        }

        protected override void OnStop()
        {
            //initialize all variables and destroy timer
            try
            {
                RSI_VALUE = 0;
                rsi = null;
                this.oTimer = null;

                LAST_BUY = 0.0;
                NEXT_BUY = 0.0;

                LAST_SELL = 0.0;
                NEXT_SELL = 0.0;

                //initialize the volumes to start trading with
                NEXT_BUY_VOLUME = 0;
                NEXT_SELL_VOLUME = 0;

                LOSE_COUNT = 0;

                this.tradeBuy = true;
                this.tradeSell = true;
            } catch (Exception ee)
            {
                Print(ee.Message + " from onStop()");
            }
        }

        #endregion


        #region Trade-Trigger

        public double determineLotSize(double EQ)
        {
            //method is responsible for determining the lot size to use for a particular trade
            try
            {
                double _val = (this.Chunk * EQ);
                double _pip = Symbol.PipSize;
                double _pipV = Symbol.PipValue;
                long ls = Symbol.LotSize;
                //this.MaxVolume = Symbol.NormalizeVolume((Account.FreeMargin / Symbol.Ask * Account.Leverage), RoundingMode.Down);

                Print("Value to trade on is {0}", _val.ToString());
                Print("Pip Size for {0} is {1}, and pip value is {2}", SymbolName, _pip.ToString(), _pipV.ToString());
                Print("1 lot in base currency is {0}", ls.ToString());
                //Print("Maximum volume to use is {0}", this.MaxVolume.ToString());
                return 0.0;

            } catch (Exception lotErr)
            {
                Print("Message " + lotErr.Message + " in determineLotSize()");
                return 0.0;
            }
        }

        private void TradeValidators()
        {
            //this.determineLotSize(Account.Equity);
            //return;
            TradeResult result;
            var posKount = Positions.FindAll(SymbolName, SymbolName);

            if (posKount.Length < this.PositionCount)
            {
                #region BUY Conditions


                if (RSI_VALUE <= 30.0)
                {
                    try
                    {

                        var list = FindTradeCountList(TradeType.Buy);
                        var LN = list.Length;

                        if ((LN == 0) && (this.tradeBuy == true))
                        {
                            result = ExecuteMarketOrder(TradeType.Buy, SymbolName, NEXT_BUY_VOLUME, SymbolName, null, null);
                            LAST_BUY = result.Position.EntryPrice;
                            NEXT_BUY = result.Position.EntryPrice - (Symbol.PipSize * this.PipSizeAttribute);

                            if (NEXT_BUY_VOLUME != 50000.0)
                            {
                                NEXT_BUY_VOLUME = (long)50000.0;
                            }


                            NEXT_SELL = 0.0;
                        }
                        else if (LN > 0)
                        {
                            if (Symbol.Bid <= NEXT_BUY)
                            {
                                result = ExecuteMarketOrder(TradeType.Buy, SymbolName, NEXT_BUY_VOLUME, SymbolName, null, null);
                                LAST_BUY = result.Position.EntryPrice;
                                NEXT_BUY = result.Position.EntryPrice - (Symbol.PipSize * this.PipSizeAttribute);


                                if (NEXT_BUY_VOLUME != (long)50000.0)
                                {
                                    NEXT_BUY_VOLUME = (long)50000.0;
                                }

                                NEXT_SELL = 0.0;
                            }
                        }

                    } catch (Exception buyEx)
                    {
                        Print(buyEx.Message + " buy Operation()");
                    }
                }

                #endregion

                //over - bought...SELL
                //CHECK IF ANY BUY CONDITION EXISTS WHICH IS IN PROFIT. CLOSE IT IF IT DOES EXIST
                if ((RSI_VALUE >= 70.0) || (RSI_VALUE >= 68.5 && rsi.Result.IsRising()))
                {
                    try
                    {
                        var slist = FindTradeCountList(TradeType.Sell);
                        var sL = slist.Length;

                        if ((sL == 0) && (this.tradeSell == true))
                        {
                            result = ExecuteMarketOrder(TradeType.Sell, SymbolName, NEXT_SELL_VOLUME, SymbolName, null, null);
                            LAST_SELL = result.Position.EntryPrice;
                            NEXT_SELL = LAST_SELL + (Symbol.PipSize * this.PipSizeAttribute);

                            if (NEXT_SELL_VOLUME != (long)50000.0)
                            {
                                NEXT_SELL_VOLUME = (long)50000.0;
                            }

                            NEXT_BUY = 0.0;
                        }

                        if ((sL > 0) && (NEXT_SELL != 0.0))
                        {
                            if (Symbol.Bid >= NEXT_SELL)
                            {
                                result = ExecuteMarketOrder(TradeType.Sell, SymbolName, NEXT_SELL_VOLUME, SymbolName, null, null);
                                LAST_SELL = result.Position.EntryPrice;
                                NEXT_SELL = LAST_SELL + (Symbol.PipSize * this.PipSizeAttribute);

                                if (NEXT_SELL_VOLUME != (long)50000.0)
                                {
                                    NEXT_SELL_VOLUME = (long)50000.0;
                                }


                                NEXT_BUY = 0.0;
                            }
                        }
                    } catch (Exception sellEx)
                    {
                        Print(sellEx.Message + " sell Operation()");
                    }
                }
            }

        }


        #endregion


        #region Position-Management
        private void ManageTradePositions(string status)
        {
            //evaluates trades of the particular type and consolidate profit
            try
            {

                //if (this.WIN_COUNT >= this.WTCount)
                //{
                if (status == string.Empty)
                {
                    //managing particular trade type
                    var trades = FindTradeCountList(this.tradeToManage);
                    if (trades.Length > 0)
                    {
                        foreach (var td in trades)
                        {
                            if (td.GrossProfit >= this.TakeProfit)
                            {
                                ClosePosition(td);
                            }

//if trade is hanging or almost going bad
                            TimeSpan t = td.EntryTime.TimeOfDay;
                            var diff = DateTime.Now.Subtract(td.EntryTime).TotalMinutes;
                            Print("Difference in time is {0}", diff.ToString());
                            if (td.NetProfit > 0 && (diff > 10))
                            {
                                ClosePosition(td);
                            }
                        }
                    }
                }
                else if (status == "*")
                {
                    //managing positions of all types
                    var pos = Positions.FindAll(SymbolName, SymbolName);
                    if (pos.Length > 0)
                    {
                        foreach (var p in pos)
                        {
                            if (p.NetProfit >= this.TakeProfit)
                            {
                                ClosePosition(p);
                            }
                        }
                    }
                }

                // this.WIN_COUNT = 0;
                //}

                //managing negative positions
                //Print("Current lose count = {0}, Winning count = {1}", this.LOSE_COUNT.ToString(), this.WIN_COUNT.ToString());

                if (this.LOSE_COUNT >= this.LTCount)
                {
                    //sort bad positions and close the biggest
//check if the drawdown value has been breached...if not, use time of trade entry to decide to close or keep position open


                    var dta = Positions.FindAll(SymbolName, SymbolName);
                    if (dta.Length > 0)
                    {
                        try
                        {
                            //sort and pick out the largest losing position to close
                            Position selected_position = null;
                            var dict = new SortedList<double, Position>();

                            foreach (var neg in dta)
                            {
                                if (neg.NetProfit < 0)
                                {
                                    dict.Add(neg.NetProfit, neg);
                                }
                            }

                            //iterate and close most costly of positions
                            foreach (var item in dict)
                            {
                                if (this.isDrawDownThresholdBreached(item.Value))
                                {
                                    selected_position = item.Value;
                                }
                                break;
                            }

                            //close the position
                            if (selected_position != null)
                            {
                                ClosePosition(selected_position);
                                this.LOSE_COUNT = 0;
                            }

                        } catch (Exception closePosErr)
                        {
                            Print(closePosErr.Message + " from ClosingPositionRoutine()");
                        }
                    }
                }

            } catch (Exception ee)
            {
                Print(ee.Message + " from ManagePositions()");
            }
        }

        #endregion


        private Position[] FindTradeCountList(TradeType t)
        {
            //gets the number of count for the trade list
            var kount = Positions.FindAll(SymbolName, SymbolName, t);
            return kount;
        }


        private bool isDrawDownThresholdBreached(Position pos)
        {
            //determines if the drawdown threshold has been breached
            double result = Math.Round(((pos.NetProfit / this.TradeBalance) * 100), 2);
            var ddValue = (this.DRAWDOWN * -1);

            ddValue = Math.Abs(ddValue);
            result = Math.Abs(result);

            if (result >= this.DRAWDOWN)
            {
                Print("Drawdown value breached. Drawdown is {0}. Base value from cBot setting is {1}", result.ToString(), ddValue.ToString());
                return true;
            }
            else
            {
                Print("Drawdown value not breached. Drawdown is currently {0}. Base value from cBot setting is {1}", result.ToString(), ddValue.ToString());
                return false;
            }
        }

    }
}
