/*
** Developer: Nana Ofosu Gyeabour Appiah

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
    public class JehovahJireh : Robot
    {

        private RelativeStrengthIndex rsi { get; set; }

        private bool tDirection;

        [Parameter("TakeProfit", DefaultValue = 0.0)]
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

        [Parameter("WTCount", DefaultValue = 5)]
        public int WTCount { get; set; }

        [Parameter("LTCount", DefaultValue = 10)]
        public int LTCount { get; set; }

        [Parameter("K Factor", DefaultValue = 10)]
        public int KFactor { get; set; }

        System.Timers.Timer oTimer = null;
        public double rsiData;

        Dictionary<double, string> entries = new Dictionary<double, string>();

        public bool TREND_FLAG;
        public double RSI_VALUE;

//determines the value at which to enter a buy or sell position in the future
        public double LAST_BUY;
        public double NEXT_BUY;
        public double NEXT_BUY_VOLUME;

        public double LAST_SELL;
        public double NEXT_SELL;
        public double NEXT_SELL_VOLUME;

        public bool tradeBuy { get; set; }
        public bool tradeSell { get; set; }


        public TradeType tradeToManage { get; set; }

        public double TradeBalance { get; set; }
        public double ddnCounter { get; set; }

        public int LOSE_COUNT { get; set; }
        //public int WIN_COUNT { get; set; }

        private Thread th;
        //beginning trade balance
        private string robotName = string.Empty;

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

                NEXT_BUY_VOLUME = 1000;
                NEXT_SELL_VOLUME = 1000;

                this.tradeBuy = true;
                this.tradeSell = true;

                LOSE_COUNT = 0;

                this.InitializeSystemTimer();
                this.rsi = Indicators.RelativeStrengthIndex(Price, RSIPeriod);
                this.TradeBalance = Account.Balance;

                this.robotName = "JehovahJireh";

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
                if (this.isLosingTradeExist())
                {
                    this.LOSE_COUNT += 1;
                }

                //manage positions using a new thread
                th = new Thread(() => ManageTradePositions("*"));
                th.Priority = ThreadPriority.Highest;
                th.Start();

            } catch (Exception x)
            {
                Print(x.Message);
            }
        }

        private bool isLosingTradeExist()
        {
            //checks if a losing position exist
            int f = 0;
            int s = 0;
            try
            {
                var tds = Positions.FindAll(this.robotName, SymbolName);
                if (tds.Length > 0)
                {
                    //int s, f = 0;
                    foreach (var t in tds)
                    {
                        if (t.NetProfit < 0)
                        {
                            f++;
                        }
                        else
                        {
                            s++;
                        }
                    }

                    if (f > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            } catch (Exception losEr)
            {
                Print(losEr.Message + " from the isLosingTradeExist()");
                return false;
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
                var DD = this.calculateDrawDown();
                this.ddnCounter = Math.Round((DD / this.TradeBalance) * 100, 2);
                //this.ddnCounter = Math.Round((((Account.Equity - this.TradeBalance) / this.TradeBalance) * 100), 2);
                ChartObjects.DrawText("DRAWDOWN", "DRAWDOWN: " + this.ddnCounter.ToString(), StaticPosition.TopCenter, Colors.Yellow);

                this.TradeValidators();

            } catch (Exception ee)
            {
                Print(ee.Message + " from tick()");
            }
        }

        private double calculateDrawDown()
        {
            //calculate drawdown value for currency pair
            double tot = 0.0;
            try
            {
                //double tot = 0.0;
                var pos = Positions.FindAll(this.robotName, SymbolName);
                if (pos != null)
                {
                    foreach (var p in pos)
                    {
                        tot += p.NetProfit;
                    }
                }

                return tot;
            } catch (Exception ee)
            {
                Print(ee.Message + "from calculateDrawDown()");
                return tot;
            }
        }

        protected override void OnBar()
        {
            //On every bar, evaluate trading conditions
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

                NEXT_BUY_VOLUME = 1000 * this.KFactor;
                NEXT_SELL_VOLUME = 1000 * this.KFactor;
                LOSE_COUNT = 0;
                //WIN_COUNT = 0;

                this.tradeBuy = true;
                this.tradeSell = true;
            } catch (Exception ee)
            {
                Print(ee.Message + " from onStop()");
            }
        }

        #endregion


        #region Trade-Trigger

        private void TradeValidators()
        {
            TradeResult result;
            var posKount = Positions.FindAll(this.robotName, SymbolName);
            //Print("Overall position count is {0}", Positions.Count.ToString());
            //Print("Total position count for the currency pair {0} is {1}", SymbolName, posKount.Length.ToString());

            if (posKount.Length < this.PositionCount)
            {
                #region BUY Conditions


                if (RSI_VALUE <= 30.0 || (RSI_VALUE <= 32.5 && rsi.Result.IsFalling()))
                {
                    try
                    {

                        var list = FindTradeCountList(TradeType.Buy);
                        var LN = list.Length;

                        if ((LN == 0) && (this.tradeBuy == true))
                        {
                            result = ExecuteMarketOrder(TradeType.Buy, SymbolName, this.getTradingVolume("BUY"), this.robotName, null, null);
                            LAST_BUY = result.Position.EntryPrice;
                            NEXT_BUY = result.Position.EntryPrice - (Symbol.PipSize * this.PipSizeAttribute);
                            if (NEXT_BUY_VOLUME >= (4000.0 * this.KFactor))
                            {
                                NEXT_BUY_VOLUME = 1000.0 * this.KFactor;
                            }
                            else
                            {
                                NEXT_BUY_VOLUME += (1000.0 * this.KFactor);
                            }
                            NEXT_SELL = 0.0;
                        }
                        else if (LN > 0)
                        {
                            if (Symbol.Bid <= NEXT_BUY)
                            {
                                result = ExecuteMarketOrder(TradeType.Buy, SymbolName, this.getTradingVolume("BUY"), this.robotName, null, null);
                                LAST_BUY = result.Position.EntryPrice;
                                NEXT_BUY = result.Position.EntryPrice - (Symbol.PipSize * this.PipSizeAttribute);

                                //this.tradeToManage = TradeType.Sell;
                                //this.ManageTradePositions(string.Empty);
                                if (NEXT_BUY_VOLUME >= (4000.0 * this.KFactor))
                                {
                                    NEXT_BUY_VOLUME = 1000.0 * this.KFactor;
                                }
                                else
                                {
                                    NEXT_BUY_VOLUME += (1000.0 * this.KFactor);
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
                            result = ExecuteMarketOrder(TradeType.Sell, SymbolName, this.getTradingVolume("SELL"), this.robotName, null, null);
                            LAST_SELL = result.Position.EntryPrice;
                            NEXT_SELL = LAST_SELL + (Symbol.PipSize * this.PipSizeAttribute);
                            if (NEXT_SELL_VOLUME >= (4000.0 * this.KFactor))
                            {
                                NEXT_SELL_VOLUME = 1000.0 * this.KFactor;
                            }
                            else
                            {
                                NEXT_SELL_VOLUME += (1000.0 * this.KFactor);
                            }
                            NEXT_BUY = 0.0;
                        }

                        if ((sL > 0) && (NEXT_SELL != 0.0))
                        {
                            if (Symbol.Bid >= NEXT_SELL)
                            {

                                result = ExecuteMarketOrder(TradeType.Sell, SymbolName, this.getTradingVolume("SELL"), this.robotName, null, null);
                                LAST_SELL = result.Position.EntryPrice;
                                NEXT_SELL = LAST_SELL + (Symbol.PipSize * this.PipSizeAttribute);

                                if (NEXT_SELL_VOLUME >= (4000.0 * this.KFactor))
                                {
                                    NEXT_SELL_VOLUME = (1000.0 * this.KFactor);
                                }
                                else
                                {
                                    NEXT_SELL_VOLUME += (1000.0 * this.KFactor);
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

        private double getTradingVolume(string strTradeType)
        {
            //method is used to get the trading volume to use for trading
            //if the account equity is less than 100 units, use a volume of 1000 (0.01 lot size) as default
            double _value = 0.0;
            switch (strTradeType)
            {
                case "BUY":
                    _value = (Account.Equity > 100 ? NEXT_BUY_VOLUME : 1000);
                    break;
                case "SELL":
                    _value = (Account.Equity > 100 ? NEXT_SELL_VOLUME : 1000);
                    break;
            }

            return _value;
        }

        #endregion


        #region Position-Management
        private void ManageTradePositions(string status)
        {
            //evaluates trades of the particular type and consolidate profit
            try
            {
                if (status == string.Empty)
                {
                    //managing particular trade type
                    var trades = FindTradeCountList(this.tradeToManage);
                    if (trades.Length > 0)
                    {
                        foreach (var td in trades)
                        {
                            if (td.NetProfit >= this.TakeProfit)
                            {
                                ClosePosition(td);
                            }

                        }
                    }
                }
                else if (status == "*")
                {
                    //managing positions of all types
                    var pos = Positions.FindAll(this.robotName, SymbolName);
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


                //Print("Current Lose count is {0} out of {1}", this.LOSE_COUNT.ToString(), this.LTCount.ToString());
                if (this.LOSE_COUNT >= this.LTCount)
                {

                    var dta = Positions.FindAll(this.robotName, SymbolName);
                    //sort losing trades and remove the biggest liability
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
            var kount = Positions.FindAll(this.robotName, SymbolName, t);
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
                this.LOSE_COUNT = 0;
                return false;
            }
        }

    }
}
