using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.UI;

using NUnit.Framework;

using Message = mRemoteUG.Messages.Message;

namespace mRemoteUG.Tests.Messages.MessageWriters;

/// <summary>
/// The pop-up is reached from whichever thread reports a message: an autosave thread, an RDP
/// callback, the PuTTY watcher. Shown there, it opened unowned and could land behind the main
/// window, and it blocked the reporting thread - the autosave one while it held the save lock.
/// </summary>
public class PopupMessageWriterTests
{
    // Rebuilt by [SetUp] before each test, so null! rather than an initializer here;
    // see ADR-0022 on when that is the right answer.
    private RecordingInvoker _invoker = null!;
    private List<ShownBox> _shown = null!;

    [SetUp]
    public void Setup()
    {
        _invoker = new RecordingInvoker();
        _shown = new List<ShownBox>();
    }

    [Test]
    public void TheBoxIsPostedToTheUiThreadNotShownOnTheCallingThread()
    {
        PopupMessageWriter writer = NewWriter(owner: null);

        writer.Write(new Message(MessageClass.ErrorMsg, "the save failed"));

        Assert.That(_shown, Is.Empty, "The box was shown on the thread that reported the message.");
        Assert.That(_invoker.Posted, Has.Count.EqualTo(1));

        _invoker.RunPostedOnUiThread();

        Assert.That(_shown, Has.Count.EqualTo(1));
        Assert.That(_shown[0].Text, Is.EqualTo("the save failed"));
        Assert.That(_shown[0].Icon, Is.EqualTo(MessageBoxIcon.Error));
    }

    [Test]
    public void TheMainFormOwnsTheBoxWhenThereIsOne()
    {
        IWin32Window mainForm = new FakeWindow();
        PopupMessageWriter writer = NewWriter(owner: mainForm);

        writer.Write(new Message(MessageClass.WarningMsg, "careful"));
        _invoker.RunPostedOnUiThread();

        Assert.That(_shown, Has.Count.EqualTo(1));
        Assert.That(_shown[0].Owner, Is.SameAs(mainForm));
        Assert.That(_shown[0].Icon, Is.EqualTo(MessageBoxIcon.Warning));
    }

    [Test]
    public void WithoutAMainFormTheBoxIsStillShownUnowned()
    {
        PopupMessageWriter writer = NewWriter(owner: null);

        writer.Write(new Message(MessageClass.InformationMsg, "note"));
        _invoker.RunPostedOnUiThread();

        Assert.That(_shown, Has.Count.EqualTo(1));
        Assert.That(_shown[0].Owner, Is.Null);
    }

    /// <summary>
    /// <see cref="ControlUiThreadInvoker"/> runs a posted action inline when it has no live
    /// control left to marshal through, which during shutdown means on the reporting thread.
    /// A box there would block that thread until clicked; the message is in the log already.
    /// </summary>
    [Test]
    public void APostThatRunsOffTheUiThreadShowsNothing()
    {
        PopupMessageWriter writer = NewWriter(owner: null);

        writer.Write(new Message(MessageClass.ErrorMsg, "too late"));
        _invoker.RunPostedOffUiThread();

        Assert.That(_shown, Is.Empty);
    }

    private PopupMessageWriter NewWriter(IWin32Window? owner)
    {
        return new PopupMessageWriter(_invoker, () => owner)
        {
            ShowMessageBox = (boxOwner, text, caption, icon) => _shown.Add(new ShownBox(boxOwner, text, icon)),
        };
    }

    private sealed record ShownBox(IWin32Window? Owner, string Text, MessageBoxIcon Icon);

    private sealed class FakeWindow : IWin32Window
    {
        public IntPtr Handle => new IntPtr(1);
    }

    /// <summary>
    /// Holds posted actions instead of running them, so a test can tell posted from shown.
    /// </summary>
    private sealed class RecordingInvoker : IUiThreadInvoker
    {
        public List<Action> Posted { get; } = new List<Action>();

        public bool OnUiThread { get; private set; }

        public void Post(Action action)
        {
            Posted.Add(action);
        }

        public void Send(Action action)
        {
            throw new InvalidOperationException("Send blocks the reporting thread; the writer must post.");
        }

        public void RunPostedOnUiThread()
        {
            RunPosted(onUiThread: true);
        }

        public void RunPostedOffUiThread()
        {
            RunPosted(onUiThread: false);
        }

        private void RunPosted(bool onUiThread)
        {
            OnUiThread = onUiThread;
            var posted = new List<Action>(Posted);
            Posted.Clear();
            foreach (Action action in posted)
            {
                action();
            }

            OnUiThread = false;
        }
    }
}
