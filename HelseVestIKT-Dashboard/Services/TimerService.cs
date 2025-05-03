using System;
using System.Windows.Threading;

namespace HelseVestIKT_Dashboard.Services
{
	public class TimerService : IDisposable
	{
		private readonly DispatcherTimer _secondTimer;
		private readonly DispatcherTimer _twoSecondTimer;
		private readonly DispatcherTimer _fiveMinuteTimer;

		private event EventHandler SecondTick;
		private event EventHandler TwoSecondTick;
		private event EventHandler FiveMinuteTick;

		public TimerService()
		{
			_secondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
			_secondTimer.Tick += (_, __) => SecondTick?.Invoke(this, EventArgs.Empty);

			_twoSecondTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
			_twoSecondTimer.Tick += (_, __) => TwoSecondTick?.Invoke(this, EventArgs.Empty);

			_fiveMinuteTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
			_fiveMinuteTimer.Tick += (_, __) => FiveMinuteTick?.Invoke(this, EventArgs.Empty);
		}

		// Disse metodene kaller legger på de eksterne callback-ene
		public void TickEverySecond(EventHandler handler) => SecondTick += handler;
		public void TickEveryTwoSeconds(EventHandler handler) => TwoSecondTick += handler;
		public void TickEveryFiveMinutes(EventHandler handler) => FiveMinuteTick += handler;

		// Starter alle timerne
		public void Start()
		{
			_secondTimer.Start();
			_twoSecondTimer.Start();
			_fiveMinuteTimer.Start();
		}

		// Stopper alle timerne (og rydder opp)
		public void Dispose()
		{
			_secondTimer.Stop();
			_twoSecondTimer.Stop();
			_fiveMinuteTimer.Stop();
		}
	}
}
