﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Depressurizer.Core.Models;
using Depressurizer.Properties;

namespace Depressurizer.Dialogs
{
    internal class ScrapeDialog : CancelableDialog
    {
        #region Fields

        private readonly ConcurrentQueue<ScrapeJob> _queue;

        private readonly ConcurrentQueue<DatabaseEntry> _results = new ConcurrentQueue<DatabaseEntry>();

        private DateTime _start;

        private string _timeLeft;

        #endregion

        #region Constructors and Destructors

        public ScrapeDialog(IEnumerable<ScrapeJob> scrapeJobs) : base(Resources.ScrapeDialog_Title, true)
        {
            _queue = new ConcurrentQueue<ScrapeJob>(scrapeJobs);
            TotalJobs = _queue.Count;
        }

        #endregion

        #region Properties

        private static Database Database => Database.Instance;

        #endregion

        #region Methods

        protected override void CancelableDialog_Load(object sender, EventArgs e)
        {
            _start = DateTime.UtcNow;
            base.CancelableDialog_Load(sender, e);
        }

        protected override void Finish()
        {
            if (Canceled || _results == null)
            {
                return;
            }

            SetText(Resources.ApplyingData);

            foreach (DatabaseEntry g in _results)
            {
                Database.Add(g);
            }

            SetText(Resources.AppliedData);
        }

        protected override void RunProcess()
        {
            bool stillRunning = true;
            while (!Stopped && stillRunning)
            {
                stillRunning = RunNextJob();
            }

            OnThreadCompletion();
        }

        protected override void UpdateText()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format(CultureInfo.CurrentCulture, Resources.ScrapedProgress, JobsCompleted, TotalJobs));

            string timeLeft = string.Format(CultureInfo.CurrentCulture, "{0}: ", Resources.TimeLeft) + "{0}";
            if (JobsCompleted > 0)
            {
                TimeSpan timeRemaining = TimeSpan.FromTicks(DateTime.UtcNow.Subtract(_start).Ticks * (TotalJobs - (JobsCompleted + 1)) / (JobsCompleted + 1));
                if (timeRemaining.TotalHours >= 1)
                {
                    _timeLeft = string.Format(CultureInfo.InvariantCulture, timeLeft, timeRemaining.Hours + ":" + (timeRemaining.Minutes < 10 ? "0" + timeRemaining.Minutes : timeRemaining.Minutes.ToString(CultureInfo.InvariantCulture)) + ":" + (timeRemaining.Seconds < 10 ? "0" + timeRemaining.Seconds : timeRemaining.Seconds.ToString(CultureInfo.InvariantCulture)));
                }
                else if (timeRemaining.TotalSeconds >= 60)
                {
                    _timeLeft = string.Format(CultureInfo.InvariantCulture, timeLeft, (timeRemaining.Minutes < 10 ? "0" + timeRemaining.Minutes : timeRemaining.Minutes.ToString(CultureInfo.InvariantCulture)) + ":" + (timeRemaining.Seconds < 10 ? "0" + timeRemaining.Seconds : timeRemaining.Seconds.ToString(CultureInfo.InvariantCulture)));
                }
                else
                {
                    _timeLeft = string.Format(CultureInfo.InvariantCulture, timeLeft, timeRemaining.Seconds + "s");
                }
            }
            else
            {
                _timeLeft = string.Format(CultureInfo.CurrentCulture, timeLeft, Resources.Unknown);
            }

            stringBuilder.AppendLine(_timeLeft);
            SetText(stringBuilder.ToString());
        }

        private bool GetNextJob(out ScrapeJob job)
        {
            return _queue.TryDequeue(out job);
        }

        private bool RunNextJob()
        {
            if (!GetNextJob(out ScrapeJob job))
            {
                return false;
            }

            DatabaseEntry newGame = new DatabaseEntry(job.Id)
            {
                AppId = job.ScrapeId
            };

            newGame.ScrapeStore(Database.LanguageCode);
            if (Stopped)
            {
                return false;
            }

            if (newGame.LastStoreScrape != 0)
            {
                _results.Enqueue(newGame);
            }

            OnJobCompletion();

            return true;
        }

        #endregion
    }
}
