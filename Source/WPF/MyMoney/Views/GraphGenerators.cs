﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Walkabout.Data;
using Walkabout.StockQuotes;
using Walkabout.Views.Controls;

namespace Walkabout.Views
{
    class TransactionGraphGenerator : IGraphGenerator
    {
        NumberFormatInfo nfi = new NumberFormatInfo();
        IEnumerable data;
        Account account;
        Category category;

        public TransactionGraphGenerator(IEnumerable data, Account account, Category category)
        {
            this.data = data;
            this.account = account;
            this.category = category;
            nfi.NumberDecimalDigits = 2;
            nfi.CurrencyNegativePattern = 0;
        }

        public bool IsFlipped
        {
            get
            {
                return ((this.account != null && this.account.Type == AccountType.Credit) ||
                        (this.category != null && this.category.Type == CategoryType.Expense));
            }
        }

        public IEnumerable<TrendValue> Generate()
        {
            if (data != null)
            {
                NumberFormatInfo nfi = new NumberFormatInfo();
                nfi.NumberDecimalDigits = 2;
                nfi.CurrencyNegativePattern = 0;

                decimal balance = this.account != null ? this.account.OpeningBalance : 0;

                foreach (object row in data)
                {
                    Transaction t = row as Transaction;
                    if (t == null) continue;
                    if (t.Status == TransactionStatus.Void) continue;

                    if (t.Account == this.account || // showing transactions for an account
                        (this.account == null)) // showing transactions by category
                    {
                        // For all regular transaction lists, we calculate the overall balance on the fly, based on each transaction amount.
                        // When we list securities - not an account - we should show the overall value of the securities instead, 
                        // which is precalculated and stored within each transaction
                        if (this.account == null && t.Investment != null)
                        {
                            balance = t.RunningBalance;
                        }
                        else
                        {
                            balance += t.GetCategorizedAmount(this.category);
                        }

                        yield return new TrendValue()
                        {
                            Date = t.Date,
                            Value = balance,
                            UserData = t
                        };
                    }
                }
            }
        }

        public string GetLabel(TrendValue item)
        {
            return item.Value.ToString("n", nfi) + "\r\n" + item.Date.ToShortDateString();
        }
    }


    class SecurityGraphGenerator : IGraphGenerator
    {
        NumberFormatInfo nfi = new NumberFormatInfo();

        StockQuoteHistory history;
        Security security;

        public SecurityGraphGenerator(StockQuoteHistory history, Security security)
        {
            this.history = history;
            this.security = security;
            nfi.NumberDecimalDigits = 2;
            nfi.CurrencyNegativePattern = 0;
        }

        public bool IsFlipped { get { return false; } }

        public IEnumerable<TrendValue> Generate()
        {
            string symbol = history.Symbol;
            foreach (var item in history.History)
            {
                decimal adjustedClose = ApplySplits(item.Close, item.Date);

                yield return new TrendValue()
                {
                    Date = item.Date,
                    Value = adjustedClose,
                    UserData = symbol
                };
            }
        }

        private decimal ApplySplits(decimal close, DateTime date)
        {
            if (this.security.StockSplits != null)
            {
                foreach (var split in this.security.StockSplits)
                {
                    if (date < split.Date && split.Numerator != 0)
                    {
                        // reverse the effect of stock split.  For example, if stock split 2 : 1 on 1/10/2010
                        // and closing price was $20 on 1/1/2010, then the effective value of that stock on 
                        // 1/1/2010 is now $10 because of the split.
                        close *= split.Denominator / split.Numerator;
                    }
                }
            }
            return close;
        }

        public string GetLabel(TrendValue item)
        {
            string symbol = (string)item.UserData;
            return symbol + "\r\n" + item.Value.ToString("n", nfi) + "\r\n" + item.Date.ToShortDateString();
        }
    }

}
