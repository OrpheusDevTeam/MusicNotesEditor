using Manufaktura.Controls.Model;
using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class AsyncMidiPlayer : IDisposable
{
    private MidiOut _midiOut;
    private CancellationTokenSource _cts;
    private List<NoteEvent> _events = new List<NoteEvent>();
    private Task _playbackTask;
    private bool _isPaused = false;
    private object _syncLock = new object();
    private bool _isDisposed = false;

    // Timing
    private int _bpm = 120;
    private int _ticksPerQuarterNote = 480;
    private double _msPerTick;

    public bool IsPlaying => _playbackTask != null &&
                           !_playbackTask.IsCompleted &&
                           !_playbackTask.IsFaulted &&
                           !_playbackTask.IsCanceled;

    public bool IsPaused => _isPaused;

    public event EventHandler PlaybackCompleted;

    public async Task PlayScoreAsync(Score score, int bpm = 120)
    {
        // Stop any existing playback first
        await StopAsync();

        lock (_syncLock)
        {
            if (_isDisposed) return;

            // Store BPM and calculate timing
            _bpm = bpm;
            _msPerTick = 60000.0 / (_bpm * _ticksPerQuarterNote);

            // Convert score to MIDI events
            ConvertScoreToMidiEvents(score);

            // Initialize MIDI output
            InitializeMidiOutput();

            // Create cancellation token
            _cts = new CancellationTokenSource();
            _isPaused = false;

            // Start playback task
            _playbackTask = Task.Run(() => PlayEventsAsync(_cts.Token));
        }
    }

    private void ConvertScoreToMidiEvents(Score score)
    {
        _events.Clear();
        long currentTime = 0;

        foreach (var staff in score.Staves)
        {
            int channel = score.Staves.IndexOf(staff) % 16;

            foreach (var measure in staff.Measures)
            {
                foreach (var element in measure.Elements)
                {
                    if (element is Note note)
                    {
                        long noteTicks = CalculateTicks(note.BaseDuration.DenominatorAsPowerOfTwo,
                                                       note.NumberOfDots);

                        // Note On
                        _events.Add(new NoteEvent(currentTime, channel + 1,
                            MidiCommandCode.NoteOn, note.MidiPitch, 100));

                        // Note Off
                        _events.Add(new NoteEvent(currentTime + noteTicks, channel + 1,
                            MidiCommandCode.NoteOff, note.MidiPitch, 0));

                        currentTime += noteTicks;
                    }
                    else if (element is Rest rest)
                    {
                        currentTime += CalculateTicks(rest.BaseDuration.DenominatorAsPowerOfTwo,
                                                     rest.NumberOfDots);
                    }
                }
            }
        }

        _events.Sort((a, b) => a.AbsoluteTime.CompareTo(b.AbsoluteTime));
    }

    private long CalculateTicks(int denominatorAsPowerOfTwo, int dots)
    {
        double quarterNotes = 4.0 / Math.Pow(2, denominatorAsPowerOfTwo);

        for (int i = 0; i < dots; i++)
            quarterNotes += quarterNotes / 2;

        return (long)(quarterNotes * _ticksPerQuarterNote);
    }

    private void InitializeMidiOutput()
    {
        // Close existing MIDI output
        _midiOut?.Dispose();
        _midiOut = null;

        try
        {
            if (MidiOut.NumberOfDevices > 0)
            {
                _midiOut = new MidiOut(0);
                Console.WriteLine("MIDI device opened successfully");
            }
            else
            {
                Console.WriteLine("No MIDI devices available");
                _midiOut = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening MIDI device: {ex.Message}");
            _midiOut = null;

            // Try to release and retry
            if (ex.Message.Contains("AlreadyAllocated"))
            {
                Thread.Sleep(100);
                try
                {
                    if (MidiOut.NumberOfDevices > 0)
                    {
                        _midiOut = new MidiOut(0);
                        Console.WriteLine("MIDI device opened on second attempt");
                    }
                }
                catch (Exception ex2)
                {
                    Console.WriteLine($"Second attempt failed: {ex2.Message}");
                }
            }
        }
    }

    private async Task PlayEventsAsync(CancellationToken cancellationToken)
    {
        if (_events.Count == 0 || _midiOut == null) return;

        int eventIndex = 0;
        long lastEventTime = 0;

        try
        {
            while (eventIndex < _events.Count && !cancellationToken.IsCancellationRequested)
            {
                // Handle pause
                while (_isPaused && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested) break;

                var ev = _events[eventIndex];

                // Calculate delay
                long delayTicks = ev.AbsoluteTime - lastEventTime;
                long delayMs = (long)(delayTicks * _msPerTick);

                if (delayMs > 0)
                {
                    await Task.Delay((int)delayMs, cancellationToken);
                }

                // Send MIDI event
                if (_midiOut != null && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        _midiOut.Send(ev.GetAsShortMessage());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error sending MIDI: {ex.Message}");
                    }
                }

                lastEventTime = ev.AbsoluteTime;
                eventIndex++;
            }
            await Task.Delay(500, cancellationToken);

        }
        finally
        {
            // Send all notes off when finished
            SendAllNotesOff();

            // Notify completion if not cancelled
            if (!cancellationToken.IsCancellationRequested && eventIndex >= _events.Count)
            {
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Pause()
    {
        lock (_syncLock)
        {
            if (IsPlaying && !_isPaused)
            {
                _isPaused = true;
                SendAllNotesOff();
            }
        }
    }

    public void Resume()
    {
        lock (_syncLock)
        {
            if (IsPlaying && _isPaused)
            {
                _isPaused = false;
            }
        }
    }

    public async Task StopAsync()
    {
        lock (_syncLock)
        {
            if (_cts == null || _playbackTask == null)
            {
                SendAllNotesOff();
                _midiOut?.Dispose();
                _midiOut = null;
                return;
            }

            _cts.Cancel();
            SendAllNotesOff();
        }

        try
        {
            // Wait for task to complete with timeout
            if (_playbackTask != null)
            {
                await Task.WhenAny(_playbackTask, Task.Delay(1000));
            }
        }
        catch { }
        finally
        {
            ResetState();
        }
    }

    private void SendAllNotesOff()
    {
        if (_midiOut == null) return;

        try
        {
            // Send All Notes Off on all channels
            for (int channel = 0; channel < 16; channel++)
            {
                int message = 0xB0 | channel; // Control Change
                message |= 123 << 8; // All Notes Off
                message |= 0 << 16; // Value
                _midiOut.Send(message);
            }
        }
        catch { }
    }

    private void ResetState()
    {
        _isPaused = false;
        _playbackTask = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            _isDisposed = true;

            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            SendAllNotesOff();
            _midiOut?.Dispose();
            _midiOut = null;
        }
    }
}