using System.Collections.Specialized;

using mRemoteUG.Messages;
using mRemoteUG.Messages.MessageWriters;
using mRemoteUG.Messages.WriterDecorators;
using mRemoteUG.UI;
using mRemoteUG.UI.Forms;

namespace mRemoteUG.App.Initialization;

public class MessageCollectorSetup
{
    /// <summary>
    /// Builds the message writers and starts feeding them, backlog first.
    /// </summary>
    /// <remarks>
    /// The writers are built here rather than by the caller because the two used to be separate
    /// calls, which left a window in which the collector had a subscriber but nothing to write
    /// to. Anything reported in that window, or before either call, went nowhere at all - see
    /// <see cref="MessageCollector.SubscribeAndReplay"/>, which hands over the backlog.
    /// </remarks>
    public static void SetupMessageCollector(MessageCollector messageCollector, IList<IMessageWriter> messageWriterList)
    {
        BuildMessageWritersFromSettings(messageWriterList);

        messageCollector.SubscribeAndReplay((o, args) => FanOut(messageWriterList, args));
    }

    /// <summary>
    /// Hands every message in <paramref name="args"/> to every writer, in order.
    /// </summary>
    /// <remarks>
    /// Separate from the subscription so that it can be tested without a collector, and so that
    /// the guard below is somewhere a reader will find it.
    /// </remarks>
    internal static void FanOut(IList<IMessageWriter> messageWriterList, NotifyCollectionChangedEventArgs args)
    {
        // The collector only ever adds, but a handler that assumed so would throw on any other
        // notification rather than ignoring it.
        if (args.Action != NotifyCollectionChangedAction.Add || args.NewItems == null)
        {
            return;
        }

        IMessage[] messages = args.NewItems.Cast<IMessage>().ToArray();
        foreach (IMessageWriter printer in messageWriterList)
        {
            foreach (IMessage message in messages)
            {
                printer.Write(message);
            }
        }
    }

    public static void BuildMessageWritersFromSettings(IList<IMessageWriter> messageWriterList)
    {
#if DEBUG
        messageWriterList.Add(BuildDebugConsoleWriter());
#endif
        messageWriterList.Add(BuildTextLogMessageWriter());
        messageWriterList.Add(BuildNotificationPanelMessageWriter());
        messageWriterList.Add(BuildPopupMessageWriter());
    }

#if DEBUG
    /// <summary>
    /// The Visual Studio Output window, filtered exactly like the log file.
    /// </summary>
    /// <remarks>
    /// This is a log, so it follows the Logging group rather than having rules of its own; it
    /// used to be registered bare, which made a DEBUG build behave differently from the build
    /// the user runs. OnlyLog messages still belong in a log, so there is no OnlyLog filter.
    /// </remarks>
    private static MessageTypeFilterDecorator BuildDebugConsoleWriter()
    {
        return new MessageTypeFilterDecorator(
            new LogMessageTypeFilteringOptions(),
            new DebugConsoleMessageWriter()
        );
    }
#endif

    /// <summary>
    /// The shared log sink, so that the collector and the teardown watchdog cannot end up
    /// filtering the log differently. See <see cref="FilteredLogWriter"/>.
    /// </summary>
    private static IMessageWriter BuildTextLogMessageWriter()
    {
        return FilteredLogWriter.Instance;
    }

    private static OnlyLogMessageFilter BuildNotificationPanelMessageWriter()
    {

        return new OnlyLogMessageFilter(
            new MessageTypeFilterDecorator(
                new NotificationPanelMessageFilteringOptions(),
                new MessageFocusDecorator(
                    AppWindows.ErrorsForm,
                    new NotificationPanelSwitchOnMessageFilteringOptions(),
                    new NotificationPanelMessageWriter(AppWindows.ErrorsForm)
                )
            )
        );
    }

    /// <remarks>
    /// The invoker is created here, and this runs from frmMain_Load, so it captures the UI thread
    /// and marshals through the main form, whose handle exists by then and lives as long as the
    /// application. Messages reported before Load are not lost for want of it: they wait in the
    /// collector's backlog and are replayed once this writer exists.
    /// </remarks>
    private static OnlyLogMessageFilter BuildPopupMessageWriter()
    {
        FrmMain mainForm = FrmMain.Default;
        return new OnlyLogMessageFilter(
            new MessageTypeFilterDecorator(
                new PopupMessageFilteringOptions(),
                new PopupMessageWriter(
                    new ControlUiThreadInvoker(mainForm),
                    () => UsableOwner(mainForm))
            )
        );
    }

    /// <summary>
    /// The main form, while it can still own a window; otherwise none.
    /// </summary>
    /// <remarks>
    /// During shutdown the form is closed and disposed while messages can still arrive, and
    /// MessageBox.Show with a disposed owner throws rather than showing anything.
    /// </remarks>
    internal static IWin32Window? UsableOwner(Form mainForm)
    {
        return mainForm.IsDisposed || mainForm.Disposing || !mainForm.IsHandleCreated ? null : mainForm;
    }
}
