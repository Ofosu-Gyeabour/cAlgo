using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
using System.Threading;


using System.Collections;
using System.Collections.Generic;


/*
This cBot was developed using two exponential moving averages: one medium, one slow
Developer: Nana Ofosu Gyeabour Appiah
Date: 22nd of June, 2021

---GER30(DAX30)

---UK100

---US500

--AUS200

---SPA35
*/

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class GoldEngine : Robot
    {

        #region Parameters
        private ExponentialMovingAverage _emaMedium;
        private ExponentialMovingAverage _emaSlow;
        private RelativeStrengthIndex _rsi;


        [Parameter("Medium Period", DefaultValue = 20)]
        public int medianPeriod { get; set; }

        [Parameter("Slow Periods", DefaultValue = 50)]
        public int slowPeriod { get; set; }

        [Parameter("Data Source")]
        public DataSeries Price { get; set; }

        [Parameter("K Factor (10,100,etc)", DefaultValue = 1)]
        public int scaleFactor { get; set; }

        [Parameter("Evaluation Period", DefaultValue = 30000)]
        public int EvaluationPeriod { get; set; }

        [Parameter("Pips Threshold", DefaultValue = 2)]
        public int PipsThreshold { get; set; }

//the number of periods to use for the calculation
        [Parameter("Analyze Periods", DefaultValue = 6)]
        public int AnalyzePeriods { get; set; }

        //the weight factor to add to every period (eg: 10th and last period = factor * 10)
        [Parameter("Weight Factor", DefaultValue = 1.0)]
        public double WeightFactor { get; set; }

//Percentage at which you can make input
        [Parameter("Percentile %", DefaultValue = 55)]
        public double Percentile { get; set; }

        [Parameter("ProfitInPips", DefaultValue = 100)]
        public double ProfitInPips { get; set; }

        [Parameter("Pips to Trigger SL", DefaultValue = 10)]
        public double PipsToTriggerSL { get; set; }

        [Parameter("Percentage Drawdown", DefaultValue = 0.04)]
        public double PercentageDrawdown { get; set; }

        [Parameter("Trailing Stop Loss", DefaultValue = 5)]
        public double TrailingStopLoss { get; set; }

        [Parameter("RSI Period", DefaultValue = 6)]
        public int RSIPeriod { get; set; }

        System.Timers.Timer oTimer = null;
        private Thread th;

        private double dblPipDifference;
        private double dblWeightF;

        private string strRobotName;
        private bool TRADE_FLAG = false;

        private bool blnTrade;
        private DateTime TradingSessionDate;

        private bool _trailingStopLossFlag;

        //true if hedging has been activated because of a losing trade
        private bool _hedgingMode;


        #endregion

        protected override void OnStart()
        {
            // Put your initialization logic here
            try
            {
                //if (Account.IsLive == false)
                //{
                this.blnTrade = true;
                this.TradingSessionDate = DateTime.Now;

                strRobotName = "IndexOnSteroids";
                this.dblPipDifference = 0.0;
                this.InitializeSystemTimer();

                _emaMedium = Indicators.ExponentialMovingAverage(Price, medianPeriod);
                _emaSlow = Indicators.ExponentialMovingAverage(Price, slowPeriod);
                this._rsi = Indicators.RelativeStrengthIndex(Price, RSIPeriod);

                //}
                //else
                //{
                //    ChartObjects.DrawText("Invalid Account", "Get in touch with the developer to activate account", StaticPosition.BottomRight, Colors.Yellow);
                //}
            } catch (Exception startErr)
            {
                //Print(startErr.Message + " onStart()");
            }
        }

        #region Custom-Methods



        private void InitializeSystemTimer()
        {
            try
            {
                this.oTimer = new System.Timers.Timer(this.EvaluationPeriod);
                this.oTimer.AutoReset = true;
                this.oTimer.Enabled = true;
                this.oTimer.Start();

                this.oTimer.Elapsed += new System.Timers.ElapsedEventHandler(this.FundManager);


            } catch (Exception e)
            {
                Print(e.Message);
            }
        }

        private void FundManager(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                //manage positions using a new thread
                if (blnTrade == true)
                {
                    th = new Thread(() => ManageTradePositions("*"));
                    th.Priority = ThreadPriority.Highest;
                    th.Start();
                }
                else
                {
                    //close all positions

                }

            } catch (Exception x)
            {
                Print(x.Message);
            }
        }

        private bool isConfirmed(string strTradeSignal)
        {
            //method is responsible for confirmation of trend
            //strTradeSignal: either  BUY or a SELL
            double tValue = 0.0;

            try
            {
                tValue = this.computePercentile(strTradeSignal);
                return tValue > this.Percentile ? true : false;

            } catch (Exception confirmedExc)
            {
                Print(confirmedExc.Message + " isConfirmed()");
                return false;
            }
        }

        private double computePercentile(string signal)
        {
            //method actually responsible for computing percentile
            try
            {
                double sum = 0.0;
                double totalSum = 0.0;

                for (int i = 1; i <= this.AnalyzePeriods; i++)
                {
                    totalSum += ((this.AnalyzePeriods - i) * WeightFactor);
                }

                if (signal == "BUY")
                {
                    //get sum for confirmation
                    for (int k = 1; k <= this.AnalyzePeriods; k++)
                    {
                        if (Price.Last(k) > Price.Last(k + 1))
                        {
                            sum += ((this.AnalyzePeriods - k) * WeightFactor);
                        }
                        else
                        {
                            sum += 0.0;
                        }
                    }
                }

                if (signal == "SELL")
                {
                    for (int k = 1; k <= this.AnalyzePeriods; k++)
                    {
                        if (Price.Last(k) < Price.Last(k + 1))
                        {
                            sum += ((this.AnalyzePeriods - k) * WeightFactor);
                        }
                        else
                        {
                            sum += 0.0;
                        }
                    }
                }

                var _percent = Math.Round(((sum / totalSum) * 100), 2);
                return (_percent);

            } catch (Exception percentileEx)
            {
                Print(percentileEx.Message + " computePercentile()");
                return 0.0;
            }
        }

        private void PlaceTrade(string strTradeSignal, double PIPf)
        {
            //method is responsible for placing the trade
            double lngVolume = 1;
            TradeResult result;
            try
            {
                var pos = Positions.FindAll(SymbolName, SymbolName);

                if ((pos.Length == 0))
                {

                    #region Modification

                    double x = (PIPf / this.PipsThreshold);

                    if ((x > 1.0) & (x <= 2.0))
                    {
                        lngVolume = lngVolume;
                    }

                    if ((x > 2.0) & (x <= 3.0))
                    {
                        if ((Account.Balance > 100) & (Account.Balance <= 100000))
                        {
                            lngVolume = (2 * lngVolume);
                        }
                    }

                    if ((x > 3.0) & (x <= 4.0))
                    {
                        if ((Account.Balance > 100) & (Account.Balance <= 100000))
                        {
                            lngVolume = (3 * lngVolume);
                        }
                    }

                    if ((x > 4.0) & (x <= 5.0))
                    {
                        if ((Account.Balance > 100) & (Account.Balance <= 100000))
                        {
                            lngVolume = (4 * lngVolume);
                        }
                    }

                    if ((x > 5.0) & (x <= 10.0))
                    {
                        if ((Account.Balance > 100) & (Account.Balance <= 100000))
                        {
                            lngVolume = (5 * lngVolume);
                        }
                    }

                    if (x > 10.0)
                    {
                        if ((Account.Balance > 100) & (Account.Balance <= 100000))
                        {
                            lngVolume = (8 * lngVolume);
                        }
                    }

                    #endregion

                    lngVolume = 2;
                    if (strTradeSignal == "BUY")
                    {
                        result = ExecuteMarketOrder(TradeType.Buy, SymbolName, lngVolume, SymbolName, null, null);
                    }
                    else
                    {
                        result = ExecuteMarketOrder(TradeType.Sell, SymbolName, lngVolume, SymbolName, null, null);
                    }

                    if (result.IsSuccessful)
                    {
                        TRADE_FLAG = true;
                    }
                }
            } catch (Exception ex)
            {
                Print(ex.Message + " PlaceTrade()");
            }
        }

        private void TradeConditions()
        {
//method spells out trading conditions or criterion to be met for a trade to be entered
            try
            {
                bool blnConfirmation = false;
                string sellFLAG = "SELL";
                string buyFLAG = "BUY";

                if (dblPipDifference >= this.PipsThreshold)
                {
                    if ((_emaSlow.Result.LastValue > _emaMedium.Result.LastValue) && (_emaMedium.Result.LastValue > Price.LastValue))
                    {
                        //if (_rsi.Result.LastValue >= 50 && _rsi.Result.IsFalling())
                        if (((_rsi.Result.LastValue <= 70) && (_rsi.Result.LastValue >= 25)) && (_rsi.Result.IsFalling()))
                        {
                            //trigger SELL confirmation routine. MAY remove the isConfirmed routine later to give more trades
                            if (this.isConfirmed(sellFLAG))
                            {
                                ChartObjects.DrawText("Market_Conditions", "EXPECT TO SELL: SHORT CONDITIONS MET ", StaticPosition.BottomRight, Colors.Yellow);
                                this.PlaceTrade(sellFLAG, dblPipDifference);
                            }
                        }
                    }

                    if (_emaMedium.Result.HasCrossedBelow(_emaSlow.Result, 0))
                    {
                        //if (_rsi.Result.LastValue >= 50 && _rsi.Result.IsFalling())
                        if (((_rsi.Result.LastValue <= 70) && (_rsi.Result.LastValue >= 25)) && (_rsi.Result.IsFalling()))
                        {
                            if (this.isConfirmed(sellFLAG))
                            {
                                ChartObjects.DrawText("Market_Conditions", "EXPECT TO SELL: SHORT CONDITIONS MET ", StaticPosition.BottomRight, Colors.Yellow);
                                this.PlaceTrade(sellFLAG, dblPipDifference);
                            }
                        }
                    }

                    if ((Price.LastValue > _emaMedium.Result.LastValue) && (_emaMedium.Result.LastValue > _emaSlow.Result.LastValue))
                    {
                        //if (_rsi.Result.LastValue <= 20 && _rsi.Result.IsRising())
                        if (((_rsi.Result.LastValue >= 35) && (_rsi.Result.LastValue < 75)) && (_rsi.Result.IsRising()))
                        {
                            if (this.isConfirmed(buyFLAG))
                            {
                                //open BUY trade
                                ChartObjects.DrawText("Market_Conditions", "EXPECT TO BUY: LONG CONDITIONS MET ", StaticPosition.BottomRight, Colors.Yellow);
                                this.PlaceTrade(buyFLAG, dblPipDifference);
                            }
                        }
                    }

                    //cross-overs
                    if (_emaSlow.Result.HasCrossedAbove(_emaMedium.Result, 0) && (Price.LastValue > _emaSlow.Result.LastValue))
                    {
                        //if (_rsi.Result.LastValue <= 20 && _rsi.Result.IsRising())
                        if (((_rsi.Result.LastValue >= 35) && (_rsi.Result.LastValue < 75)) && (_rsi.Result.IsRising()))
                        {
                            //trigger BUY confirmation routine
                            if (this.isConfirmed(buyFLAG))
                            {
                                ChartObjects.DrawText("Market_Conditions", "EXPECT TO BUY: LONG CONDITIONS MET ", StaticPosition.BottomRight, Colors.Yellow);
                                this.PlaceTrade(buyFLAG, dblPipDifference);
                            }
                        }
                    }
                }
            } catch (Exception tradeExc)
            {
                Print(tradeExc.Message + " TradeConditions()");
            }
        }

        void OnPositionsOpened(PositionOpenedEventArgs args)
        {
            //activated on opening a position
            if (!_hedgingMode)
            {

            }
            else
            {
                //hedging mode has been activated

            }
        }

        private void ManageTradePositions(string stat)
        {
            //Position and Risk Management takes place here
            Print("ManageTradePositions method");
            var pos = Positions.FindAll(SymbolName);
            foreach (var p in pos)
            {

                if (!_trailingStopLossFlag & (p.Pips > this.PipsToTriggerSL))
                {
                    //set flag to true
                    _trailingStopLossFlag = true;
                    Print("Stop loss flag triggered for {0}", SymbolName);
                }

                if (_trailingStopLossFlag & (p.Pips > this.PipsToTriggerSL))
                {
                    //pip threshold met. update stop loss to secure profits gotten
                    if (p.TradeType == TradeType.Buy)
                    {
                        var newSLPrice = Symbol.Ask - (Symbol.PipSize * this.TrailingStopLoss);
                        ModifyPositionAsync(p, newSLPrice, null, true);
                    }
                    else if (p.TradeType == TradeType.Sell)
                    {
                        var newSLPrice = Symbol.Bid + (Symbol.PipSize * this.TrailingStopLoss);
                        ModifyPositionAsync(p, newSLPrice, null, true);
                    }
                }

                if (p.NetProfit >= this.ProfitInPips)
                {
                    Print("About to close a position");
                    ClosePosition(p);
                }

//this is for STOP LOSS. Take a look at the calculation again
                if (p.NetProfit < 0)
                {
                    var bln = this.getDrawdownPercentage(p);
                    if (bln)
                    {
                        ClosePosition(p);
                    }
                }
            }
        }

        private bool getDrawdownPercentage(Position p)
        {
            //method is responsible for getting the percentage drawdown
            try
            {
                var _value = (p.NetProfit);
                var _rat = (this.PercentageDrawdown * Account.Balance);

                Print("Absolute loss value = {0}", _value.ToString());
                Print("Stop Loss threshold at {0} of Account Balance = {1}", this.PercentageDrawdown.ToString(), _rat.ToString());
                return false;
                //return _rat >= _value ? true : false;
            } catch (Exception x)
            {
                return false;
            }
        }
        #endregion


        protected override void OnTick()
        {
            try
            {

                ChartObjects.DrawText("MEDIUM MA|VALUE", "MEDIUM MA ( " + this.medianPeriod.ToString() + " )" + Math.Round(this._emaMedium.Result.LastValue, 5).ToString(), StaticPosition.TopRight, Colors.Yellow);
                ChartObjects.DrawText("SLOW MA|VALUE", "SLOW MA ( " + this.slowPeriod.ToString() + " )" + Math.Round(this._emaSlow.Result.LastValue, 5).ToString(), StaticPosition.TopCenter, Colors.Yellow);
                ChartObjects.DrawText("LAST PRICE", "LAST PRICE " + this.Price.LastValue.ToString(), StaticPosition.TopLeft, Colors.Yellow);

                dblPipDifference = Math.Round((Math.Abs(this.Price.LastValue - this._emaMedium.Result.LastValue) / Symbol.PipSize), 5);

                ChartObjects.DrawText("PIPS DIFF", "DIFF BTN PRICE AND FAST MA IN PIPS: " + dblPipDifference.ToString(), StaticPosition.BottomCenter, Colors.Yellow);
                ChartObjects.DrawText("Market_Conditions", "MARKET CONDITIONS...ANALYZING ", StaticPosition.BottomRight, Colors.Yellow);

                //all the job gets done in this procedure/method


                this.TradeConditions();
            } catch (Exception onTickExc)
            {
                Print(onTickExc.Message + " onTick()");
            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
            try
            {

            } catch (Exception ee)
            {
            }
        }

        protected override void OnBar()
        {
            //calculate weight factor on every bar
            try
            {

            } catch (Exception e)
            {
                Print(e.Message + " onBar()");
            }
        }
    }
}
