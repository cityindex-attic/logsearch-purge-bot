using System;
using System.Threading;
using log4net;

namespace PurgeBot
{
    public class PurgeRunner
    {
        private static ILog _logger = LogManager.GetLogger(typeof (PurgeRunner));
        private TimeSpan _frequency;
        private DateTime _toDate;
        private Uri _uri;

        private Timer _timer;
        private bool _isRunning;
        private bool _isBusy;
        public PurgeRunner(TimeSpan frequency, DateTime toDate, Uri uri)
        {
            _frequency = frequency;
            _toDate = toDate;
            _uri = uri;
        }

        public void Start()
        {
            if (_isRunning)
            {
                return;
            }


            _timer = new Timer(PurgeTimerCallback, null, TimeSpan.FromSeconds(10), _frequency);

            _isRunning = true;

        }



        private void PurgeTimerCallback(object ignore)
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;

            try
            {
                var job = new PurgeJob(_toDate, _uri);
                job.Execute();
            }
            finally
            {
                _isBusy = false;
            }
        }


        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            var disposed = new ManualResetEvent(false);
            _logger.Info("waiting for timer to exit");
            _timer.Dispose(disposed);
            disposed.WaitOne();
            _logger.Info("timer stopped. schedule cancelled.");
            _isRunning = false;
        }



        ////http://skysanders.net/subtext/archive/2010/04/20/c-truncate-datetime.aspx
        /// <summary>
        ///     <para>Truncates a DateTime to a specified resolution.</para>
        ///     <para>A convenient source for resolution is TimeSpan.TicksPerXXXX constants.</para>
        /// </summary>
        /// <param name="date">The DateTime object to truncate</param>
        /// <param name="resolution">e.g. to round to nearest second, TimeSpan.TicksPerSecond</param>
        /// <returns>Truncated DateTime</returns>
        public static DateTime TruncateDateTime(DateTime date, long resolution)
        {
            return new DateTime(date.Ticks - (date.Ticks % resolution), date.Kind);
        }
    }
}