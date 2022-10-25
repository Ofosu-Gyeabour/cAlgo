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
Date: 23rd of October, 2023
*/

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MATrendCatcher : Robot
    {

        #region Parameters
        private ExponentialMovingAverage _emaMedium;
        private ExponentialMovingAverage _emaSlow;
        private RelativeStrengthIndex _rsi;

        [Parameter("Fast Period",Group ="Moving Averages and RSI", DefaultValue = 20)]
        public int FastPeriod { get; set; }

        [Parameter("Slow Periods",Group ="Moving Averages and RSI", DefaultValue = 50)]
        public int SlowPeriod { get; set; }

        [Parameter("Data Source", Group ="Moving Averages and RSI")]
        public DataSeries Price { get; set; }

        [Parameter("K Factor (10,100,etc)",Group ="Analysis", DefaultValue = 1)]
        public int ScaleFactor { get; set; }


        [Parameter("Pips Threshold",Group ="Targets", DefaultValue = 2)]
        public int PipsThreshold { get; set; }


        //the weight factor to add to every period (eg: 10th and last period = factor * 10)
        [Parameter("Weight Factor",Group ="Analysis", DefaultValue = 1.0)]
        public double WeightFactor { get; set; }

//the number of periods to use for the calculation
        [Parameter("Analyze Periods",Group ="Targets", DefaultValue = 6)]
        public int AnalyzePeriods { get; set; }

//Percentage at which you can make input
        [Parameter("Percentile %",Group="Analysis", DefaultValue = 55)]
        public double Percentile { get; set; }

        [Parameter("ProfitInPips",Group ="Targets", DefaultValue = 100)]
        public double ProfitInPips { get; set; }

        [Parameter("Pips to Trigger SL",Group ="Targets", DefaultValue = 25)]
        public double PipsToTriggerSL { get; set; }

        [Parameter("Percentage Drawdown",Group ="Targets", DefaultValue = 0.015)]
        public double PercentageDrawdown { get; set; }

        [Parameter("Trailing Stop Loss",Group ="Targets", DefaultValue = 15)]
        public double TrailingStopLoss { get; set; }

        [Parameter("Reward-To-Risk", Group ="Targets",DefaultValue = 1.5)]
        public double RewardToRisk{get;set;}

        [Parameter("RSI Period",Group ="Moving Averages and RSI", DefaultValue = 6)]
        public int RSIPeriod { get; set; }

        public enum EnumVolume{Thousand,TenThousand,HundredThousand};
        [Parameter("Trading Volume",Group = "Trading Lots", DefaultValue =EnumVolume.TenThousand)]
        public EnumVolume EnumV{get;set;}

        System.Timers.Timer oTimer = null;
        private Thread th;

        private double dblPipDifference;
        private double dblWeightF;

        private string strRobotName;
        private bool TRADE_FLAG = false;

        private bool blnTrade;
        private DateTime TradingSessionDate;

        private bool _trailingStopLossFlag;
        private double currentSLPrice { get; set; }
        //the current stop loss price
        //true if hedging has been activated because of a losing trade
        private bool _hedgingMode;

        private int AcctNumber { get; set; }
        private double SLValue = 0.0;
        //stop loss value
        #endregion

        protected override void OnStart()
        {
            // Put your initialization logic here
          
            int acct = 0;
            try
            {
                if (ValidateUserLicense(this.AcctNumber) || ValidateUserLicense(acct))
                {
                    this.blnTrade = true;
                    this.TradingSessionDate = DateTime.Now;
    
                    strRobotName = "MATrendCatcher";
                    this.dblPipDifference = 0.0;
                    this.currentSLPrice = 0.0;
    
                    _emaMedium = Indicators.ExponentialMovingAverage(Price, FastPeriod);
                    _emaSlow = Indicators.ExponentialMovingAverage(Price, SlowPeriod);
                    this._rsi = Indicators.RelativeStrengthIndex(Price, RSIPeriod);
                }
                else
                {
                //not activated
                    ChartObjects.DrawText("Account Not Activated", "Get in touch with developer to activate account", StaticPosition.BottomRight, Colors.Yellow);
                }

            } catch (Exception)
            {
                //Print(startErr.Message + " onStart()");
            }
        }

        private bool ValidateUserLicense(int N_LICENSE)
        {
            //this method will make an API call in the future to determine if account holder has paid
            //his or her subscriptin
            //for now, return true
            
            return true;
        }

        #region Custom-Methods
        
        private bool IsConfirmed(string strTradeSignal)
        {
            //confirm BUY or SELL signal
            double TValue;

            try
            {
                TValue = this.ComputePercentile(strTradeSignal);
                if (TValue > this.Percentile){return true;} else{ return false;}

            } catch (Exception confirmedExc)
            {
                Print(confirmedExc.Message + " isConfirmed()");
                return false;
            }
        }

        private double ComputePercentile(string signal)
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

        private void PlaceTrade(string strTradeSignal, double PIPf, double SL, double PfT)
        {
            //method is responsible for placing the trade
            int lngVolume = this.GetTradingVolume();
            TradeResult result;
            try
            {
                var pos = Positions.FindAll("TrendCatcher",SymbolName);

                //trades are entered one at a time. MULTIPLE trades disallowed unless _hedgingMode = TRUE

                if ((pos.Length == 0))
                {
                    double x = (PIPf / this.PipsThreshold);

                    if (Account.Balance <= 200){
                        lngVolume = 1000;
                    }
                    
                    if (Account.Balance > 500)
                    {
                        lngVolume *= this.ScaleFactor;
                    }
                    
                    decimal RiskAmt = this.GetDrawdownAmountValue();
                    decimal PipValue = this.GetPipValue(lngVolume);
                    decimal RiskPips = RiskAmt / PipValue;
                    
                    Print("Amount to risk is {0}",RiskAmt.ToString());
                    Print("Pip value is {0}",PipValue.ToString());
                    Print("Pips to stop loss is {0}",RiskPips.ToString());
                    
                    this.ProfitInPips = (Convert.ToDouble(RiskPips) * this.RewardToRisk);
                    Print("Reward-To-Risk Pips is {0}",this.ProfitInPips.ToString());
                    
                    SL = Convert.ToDouble(RiskPips / 2);
                    this.ProfitInPips = (Convert.ToDouble(RiskPips) * this.RewardToRisk);
                    
                    //Stop();
                    //determine pip value. determine risk to take, determine take profit
                    //profit in pips, lngVolume, percentage risk
                    
                    if (strTradeSignal == "BUY")
                    {
                        result = ExecuteMarketOrder(TradeType.Buy,SymbolName,lngVolume,@"TrendCatcher",SL,ProfitInPips,@"TrendCatcher",true);
                    }
                    else
                    {
                        result = ExecuteMarketOrder(TradeType.Sell, SymbolName, lngVolume, @"TrendCatcher", SL, ProfitInPips,@"TrendCatcher",true);
                    }

                    if (result.IsSuccessful)
                    {
                        TRADE_FLAG = true;
                    }
                }
                else{
                    //modify trailing stop losses
                    this.ManageTrailingLoss();
                }
            } catch (Exception){}
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
                    if ((_emaSlow.Result.LastValue > _emaMedium.Result.LastValue) && (_emaMedium.Result.LastValue > Price.LastValue) || (_emaMedium.Result.HasCrossedBelow(_emaSlow.Result, 0)))
                    {
                        //trigger SELL confirmation routine. MAY remove the isConfirmed routine later to give more trades
                        if (((_rsi.Result.LastValue <= 70) && (_rsi.Result.LastValue >= 25)) && (_rsi.Result.IsFalling()))
                        {
                            if (this.IsConfirmed(sellFLAG))
                            {
                                //compute stopLoss and TakeProfit
                                double StopLoss = Bars.LastBar.Low + 0.4 * (Bars.LastBar.High - Bars.LastBar.Low);
                                double ProfitTarget = Bars.LastBar.Low - 0.8 * (Bars.LastBar.High - Bars.LastBar.Low);
                    
                                ChartObjects.DrawText("Market_Conditions", "EXPECT TO SELL: SHORT CONDITIONS MET ", StaticPosition.BottomRight, Colors.Yellow);
                                this.PlaceTrade(sellFLAG, dblPipDifference, StopLoss,ProfitTarget);
                            }
                        }
                    }

                    if ((Price.LastValue > _emaMedium.Result.LastValue) && (_emaMedium.Result.LastValue > _emaSlow.Result.LastValue) || (_emaSlow.Result.HasCrossedAbove(_emaMedium.Result, 0) && (Price.LastValue > _emaSlow.Result.LastValue)))
                    {
                        if (((_rsi.Result.LastValue >= 35) && (_rsi.Result.LastValue < 75)) && (_rsi.Result.IsRising()))
                        {
                            if (this.IsConfirmed(buyFLAG))
                            {
                                //open BUY trade. compute stopLoss and TakeProft
                                double StopLoss = Bars.LastBar.Low - 0.4 * (Bars.LastBar.High - Bars.LastBar.Low);
                                double ProfitTarget = Bars.LastBar.Low + 0.8 * (Bars.LastBar.High - Bars.LastBar.Low);
                    
                                ChartObjects.DrawText("Market_Conditions", "EXPECT TO BUY: LONG CONDITIONS MET ", StaticPosition.BottomRight, Colors.Yellow);
                                this.PlaceTrade(buyFLAG, dblPipDifference,StopLoss,ProfitTarget);
                            }
                        }
                    }
                }
            } catch (Exception tradeExc)
            {
                Print(tradeExc.Message + " TradeConditions()");
            }
        }
        
        private decimal GetDrawdownAmountValue()
        {
            //computes drawdown. converts percentage to actual value net balance at the time of potential price entry
            //returns decimal
            try
            {
                return Convert.ToDecimal(Account.Balance * this.PercentageDrawdown);
            } catch (Exception)
            {
                return 0;
            }
        }
        
        private decimal GetPipValue(int TradingVol){
            //gets pip value for the currently trading forex pair
            try
            {
                return Convert.ToDecimal((Symbol.PipSize * TradingVol) / this.Price.LastValue);
            }
            catch(Exception){
                return 0;
            }
        }
        
        private int GetTradingVolume(){
            //gets the trading volume selected by user
            try
            {
                switch(EnumV.ToString()){
                    case "Thousand":
                    return 1000;
                    
                    case "TenThousand":
                    Print("I am in the ten thousand zone");
                    return 10000;
                    
                    default:
                    return 100000;
                }    
            }
            catch(Exception){
                return 1000;
            }
        }
        
        #endregion


        protected override void OnTick()
        {
            try
            {

                ChartObjects.DrawText("MEDIUM MA|VALUE", "MEDIUM MA ( " + this.FastPeriod.ToString() + " )" + Math.Round(this._emaMedium.Result.LastValue, 5).ToString(), StaticPosition.TopRight, Colors.Yellow);
                ChartObjects.DrawText("SLOW MA|VALUE", "SLOW MA ( " + this.SlowPeriod.ToString() + " )" + Math.Round(this._emaSlow.Result.LastValue, 5).ToString(), StaticPosition.TopCenter, Colors.Yellow);
                ChartObjects.DrawText("LAST PRICE", "LAST PRICE " + this.Price.LastValue.ToString(), StaticPosition.TopLeft, Colors.Yellow);
                ChartObjects.DrawText("RSI VALUE", "RSI VALUE " + this._rsi.Result.LastValue.ToString(), StaticPosition.BottomLeft, Colors.Yellow);

                dblPipDifference = Math.Round((Math.Abs(this.Price.LastValue - this._emaMedium.Result.LastValue) / Symbol.PipSize), 5);

                ChartObjects.DrawText("PIPS DIFF", "DIFF BTN PRICE AND FAST MA IN PIPS: " + dblPipDifference.ToString(), StaticPosition.BottomCenter, Colors.Yellow);
                ChartObjects.DrawText("Market_Conditions", "MARKET CONDITIONS...ANALYZING ", StaticPosition.BottomRight, Colors.Yellow);

                //all the job gets done in this procedure/method
                this.ManageTrailingLoss();
                this.TradeConditions();
            } catch (Exception onTickExc)
            {
                Print(onTickExc.Message + " onTick()");
            }
        }


        private void ManageTrailingLoss(){
            //manages trailing loss for a position
            var position = Positions.Find("TrendCatcher",SymbolName);
            if (position == null){
                return;
            }
            
            if (position.Pips >= this.PipsToTriggerSL){
                if (position.TradeType == TradeType.Buy){
                    var newSLPrice = Symbol.Ask - (Symbol.PipSize * this.TrailingStopLoss);
                    if (newSLPrice > position.StopLoss){
                        ModifyPosition(position,newSLPrice,position.TakeProfit);
                    }
                }
                else
                {
                    var newSLPrice = Symbol.Bid + (Symbol.PipSize * this.TrailingStopLoss);
                    if (newSLPrice < position.StopLoss){
                        ModifyPosition(position,newSLPrice,position.TakeProfit);    
                    }
                }
            }
        }

        protected override void OnStop()
        {
            // Put your deinitialization logic here
            try
            {
                
            } catch (Exception){}
        }
    }
}
