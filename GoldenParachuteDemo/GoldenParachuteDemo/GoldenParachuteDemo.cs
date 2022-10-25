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
    public class GoldenParachuteDemo : Robot
    {

        #region Parameters
        private ExponentialMovingAverage _emaMedium;
        private ExponentialMovingAverage _emaSlow;
        private RelativeStrengthIndex _rsi { get; set; }

        //[Parameter("Medium Period", DefaultValue = 20)]
        private int medianPeriod = 20;

        //[Parameter("Slow Periods", DefaultValue = 50)]
        private int slowPeriod = 50;

        private double RSI_VALUE { get; set; }

        [Parameter("Data Source")]
        public DataSeries Price { get; set; }

        private int scaleFactor = 1;

        [Parameter("Evaluation Period", DefaultValue = 15000)]
        public int EvaluationPeriod { get; set; }

        [Parameter("Expected Profit", DefaultValue = 40)]
        public double ExpectedProfit { get; set; }

        [Parameter("TakeLossInPips", DefaultValue = 500)]
        public double TakeLossInPips { get; set; }

        [Parameter("Lots (1 - 100)", DefaultValue = 100)]
        public double lngVolume { get; set; }

        [Parameter("Analyze Period", DefaultValue = 5)]
        public int RSIPeriod { get; set; }

        //[Parameter("Pips Threshold", DefaultValue = 2)]
        private int PipsThreshold = 2;

        private int AnalyzePeriods = 3;

        private double WeightFactor = 1.0;

        //Percentage at which you can make input
        private double Percentile = 65;

        [Parameter("ProfitInPips", DefaultValue = 100)]
        public double ProfitInPips { get; set; }

        //[Parameter("Pips to Trigger SL", DefaultValue = 10)]
        private double PipsToTriggerSL = 20;

        [Parameter("Percentage Drawdown", DefaultValue = 0.04)]
        public double PercentageDrawdown { get; set; }

        //[Parameter("Trailing Stop Loss", DefaultValue = 5)]
        private double TrailingStopLoss = 10;

        System.Timers.Timer oTimer = null;
        private Thread th;

        private double dblPipDifference;
        private double dblWeightF;

        private bool TRADE_FLAG = false;

        private bool blnTrade;
        private DateTime TradingSessionDate;

        private bool _trailingStopLossFlag;
        private string strRobotName = "GoldenParachuteDemo";
        private double totalProfit = 0.0;
        private double pastProfit = 0.0;
        private double currentTotPositon = 0.0;

        private bool targetReached = false;
        private bool blnLoss = false;

        //target profit flag

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

                this.dblPipDifference = 0.0;

                this.currentTotPositon = 0.0;
                this.pastProfit = 0.0;

                this.InitializeSystemTimer();

                this._rsi = Indicators.RelativeStrengthIndex(Price, RSIPeriod);
                _emaMedium = Indicators.ExponentialMovingAverage(Price, medianPeriod);
                _emaSlow = Indicators.ExponentialMovingAverage(Price, slowPeriod);

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

        private void PlaceTrade(string strTradeSignal, double PIPf, bool blnTradeF)
        {
            //method is responsible for placing the trade
            TradeResult result;
            try
            {
                var pos = Positions.FindAll(strRobotName, SymbolName);

                if ((pos.Length == 0))
                {

                    #region Modification

                    double x = (PIPf / this.PipsThreshold);

                    #endregion

                    if (blnTradeF)
                    {
                        lngVolume *= 2;
                    }
                    else
                    {
                        lngVolume = 1;
                    }

                    if (strTradeSignal == "BUY")
                    {
                        result = ExecuteMarketOrder(TradeType.Buy, SymbolName, lngVolume, strRobotName, null, null);
                    }
                    else
                    {
                        result = ExecuteMarketOrder(TradeType.Sell, SymbolName, lngVolume, strRobotName, null, null);
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
                    //add RSI to the conditional statement
                    if ((_emaSlow.Result.LastValue > _emaMedium.Result.LastValue) && (_emaMedium.Result.LastValue > Price.LastValue))
                    {
                        //if (_rsi.Result.LastValue >= 50 && _rsi.Result.IsFalling())
                        if (((_rsi.Result.LastValue <= 70) && (_rsi.Result.LastValue >= 25)) && (_rsi.Result.IsFalling()))
                        {
                            //trigger SELL confirmation routine. MAY remove the isConfirmed routine later to give more trades
                            if (this.isConfirmed(sellFLAG))
                            {
                                ChartObjects.DrawText("Market_Conditions", "EXPECT TO SELL: SHORT CONDITIONS MET ", StaticPosition.BottomRight, Colors.Yellow);
                                this.PlaceTrade(sellFLAG, dblPipDifference, this.blnLoss);
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
                                this.PlaceTrade(sellFLAG, dblPipDifference, this.blnLoss);
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
                                this.PlaceTrade(buyFLAG, dblPipDifference, this.blnLoss);
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
                                this.PlaceTrade(buyFLAG, dblPipDifference, this.blnLoss);
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
            //sets trading session date and time
            this.TradingSessionDate = DateTime.Now;
            Print("Session date and time: {0}", this.TradingSessionDate.ToShortDateString());
        }

        void OnPositionsClosed(PositionOpenedEventArgs args)
        {
            if (args.Position.NetProfit > 0)
            {
                this.blnLoss = false;
            }
            else
            {
                this.blnLoss = true;
            }
        }

        private double getValueOfOpenedPositions()
        {
            //gets the value of all opened positions belonging to the label, by the robot
            double openedTot_ = 0.0;
            try
            {
                var pos = Positions.FindAll(strRobotName, SymbolName);

                if (pos.Length > 0)
                {
                    foreach (var ps in pos)
                    {

                        openedTot_ = openedTot_ + ps.NetProfit;
                    }

                    return openedTot_;
                }
                else
                {
                    return openedTot_;
                }
            } catch
            {
                return openedTot_;
            }
        }

        private void CloseOpenPositions()
        {
            //close all opened positions
            try
            {
                var pos = Positions.FindAll(strRobotName, SymbolName);
                if (pos.Length > 0)
                {
                    foreach (var ps in pos)
                    {
                        ClosePosition(ps);
                    }
                }
            } catch
            {
            }
        }

        private void BookKeeper(TradeResult r)
        {
            try
            {
                if (r.IsSuccessful)
                {
                    this.pastProfit += r.Position.NetProfit;
                    if (r.Position.NetProfit < 0)
                    {
                        blnLoss = true;
                    }
                    else
                    {
                        blnLoss = false;
                    }
                    if ((this.pastProfit > this.ExpectedProfit) & (this.pastProfit + this.currentTotPositon) >= this.ExpectedProfit)
                    {
                        this.CloseOpenPositions();
                        this.targetReached = true;
                    }
                }
            } catch
            {
            }
        }

        private void ManageTradePositions(string stat)
        {
            //Position and Risk Management takes place here

            var pos = Positions.FindAll(strRobotName, SymbolName);
            foreach (var p in pos)
            {

                if (!_trailingStopLossFlag & (p.Pips > this.PipsToTriggerSL))
                {
                    //set flag to true
                    _trailingStopLossFlag = true;
                }

                if (_trailingStopLossFlag & (p.Pips > this.PipsToTriggerSL))
                {
                    //pip threshold met. update stop loss to secure profits gotten
                    if (p.TradeType == TradeType.Buy)
                    {
                        var newSLPrice = Symbol.Ask - (Symbol.PipSize * this.TrailingStopLoss);
                        Print("New SL Price for BUY = {0}", newSLPrice.ToString());
                        //ModifyPositionAsync(p, newSLPrice, null, true);
                    }
                    else if (p.TradeType == TradeType.Sell)
                    {
                        var newSLPrice = Symbol.Bid + (Symbol.PipSize * this.TrailingStopLoss);
                        Print("New SL Price for SELL = {0}", newSLPrice.ToString());

                        //ModifyPositionAsync(p, newSLPrice, null, true);
                    }
                }

                if (p.Pips >= (this.ProfitInPips))
                {
                    //closing the position
                    ClosePositionAsync(p, BookKeeper);
                }

                if ((p.NetProfit > 0) && (dblPipDifference < this.PipsThreshold))
                {
                    //trend reversal
                    if (p.TradeType == TradeType.Buy)
                    {
                        ClosePositionAsync(p, BookKeeper);
                        Thread.Sleep(EvaluationPeriod);
                        ExecuteMarketOrder(TradeType.Sell, SymbolName, p.VolumeInUnits, strRobotName);
                    }
                    else
                    {
                        ClosePositionAsync(p, BookKeeper);
                        Thread.Sleep(EvaluationPeriod);
                        ExecuteMarketOrder(TradeType.Buy, SymbolName, p.VolumeInUnits, strRobotName);
                    }

                    //original code
                    //ClosePositionAsync(p, BookKeeper);
                    //Thread.Sleep(EvaluationPeriod);
                }

//this is for STOP LOSS. Take a look at the calculation again
                if (p.NetProfit < 0)
                {
                    if (Math.Abs(p.Pips) >= this.TakeLossInPips)
                    {
                        ClosePositionAsync(p, BookKeeper);
                    }
                }



            }
        }
        /*
                if (p.NetProfit < 0)
                {
                    var bln = this.getDrawdownPercentage(p);
                    if (bln)
                    {
                        ClosePosition(p);
                    }
                }
                */

        private bool getDrawdownPercentage(Position p)
        {
            //method is responsible for getting the percentage drawdown
            try
            {
                var _value = (p.NetProfit * -1);
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
                RSI_VALUE = Math.Round(_rsi.Result.LastValue, 2);

                ChartObjects.DrawText("RSI DATA|TREND", RSI_VALUE.ToString() + " | " + (_rsi.Result.IsRising() ? "RISING" : "FALLING"), StaticPosition.BottomCenter, Colors.Yellow);

                ChartObjects.DrawText("MEDIUM MA|VALUE", "MEDIUM MA ( " + this.medianPeriod.ToString() + " )" + Math.Round(this._emaMedium.Result.LastValue, 5).ToString(), StaticPosition.TopRight, Colors.Yellow);
                ChartObjects.DrawText("SLOW MA|VALUE", "SLOW MA ( " + this.slowPeriod.ToString() + " )" + Math.Round(this._emaSlow.Result.LastValue, 5).ToString(), StaticPosition.TopCenter, Colors.Yellow);
                ChartObjects.DrawText("LAST PRICE", "LAST PRICE " + this.Price.LastValue.ToString(), StaticPosition.TopLeft, Colors.Yellow);

                dblPipDifference = Math.Round((Math.Abs(this.Price.LastValue - this._emaMedium.Result.LastValue) / Symbol.PipSize), 5);

                //ChartObjects.DrawText("PIPS DIFF", "DIFF BTN PRICE AND FAST MA IN PIPS: " + dblPipDifference.ToString(), StaticPosition.BottomCenter, Colors.Yellow);
                ChartObjects.DrawText("Market_Conditions", "MARKET CONDITIONS...ANALYZING ", StaticPosition.BottomRight, Colors.Yellow);

                this.currentTotPositon = Math.Round(getValueOfOpenedPositions(), 5);
                //this.pastProfit = Math.Round((this.pastProfit + this.currentTotPositon), 5);

                string POSITION_STATUS = string.Format("Past Profit = {0}, Current Position = {1}, Total Profit = {2},cBot status = {3}", this.pastProfit.ToString(), this.currentTotPositon.ToString(), (this.pastProfit + this.currentTotPositon).ToString(), "Trading");
                ChartObjects.DrawText("Total Session Profit", POSITION_STATUS, StaticPosition.BottomLeft, Colors.Yellow);

                //all the job gets done in this procedure/method

                if (this.targetReached == false)
                {
                    this.TradeConditions();

                    this.ManageTradePositions(string.Empty);
                }
                else
                {
                    //stop the robot
                    string POSITION_CLOSED = string.Format("Past Profit = {0}, Current Position = {1}, Total Profit = {2},cBot status = {3}", this.pastProfit.ToString(), this.currentTotPositon.ToString(), (this.pastProfit + this.currentTotPositon).ToString(), "Stopped");
                    ChartObjects.DrawText("Total Session Profit", POSITION_CLOSED, StaticPosition.BottomLeft, Colors.Yellow);
                    //Stop();
                }
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
