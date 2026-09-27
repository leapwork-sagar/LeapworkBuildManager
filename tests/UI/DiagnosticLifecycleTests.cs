using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using LeapworkBuildManager;

static partial class HardeningTests
{
    static void UiChecks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "Hardening-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var handler = new Handler
        {
            Reply = r => new HttpResponseMessage(HttpStatusCode.OK)
        };
        var form = new MainForm(Path.Combine(dir, "prefs.xml"), new BuildService(handler));
        form.Dispose();
        form.Dispose();
        Assert(handler.Disposals == 1, "direct form disposal releases transport exactly once");
        var field = typeof(MainForm).GetField("persistentLog", BindingFlags.Instance | BindingFlags.NonPublic);
        ((DiagnosticLog)field.GetValue(form)).CompleteAsync().GetAwaiter().GetResult();
        using(var checkedForm = new MainForm(Path.Combine(dir,"checked.xml"),new BuildService(new Handler {Reply=r=>new HttpResponseMessage(HttpStatusCode.OK)}))) {
            checkedForm.Show();
            Func<string,object> get=name=>typeof(MainForm).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(checkedForm);
            Action<Func<bool>> until = UiTestPump.Until;
            until(()=>(bool)get("preferencesLoaded"));
            ((TextBox)get("build")).Text="2026.2.257";
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            var check=(Task)typeof(MainForm).GetMethod("CheckBuildAsync",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(checkedForm,null);
            until(()=>check.IsCompleted);check.GetAwaiter().GetResult();
            var events=(System.Collections.Generic.List<DiagnosticEvent>)get("diagnostics");
            Assert(events.Count(x=>x.Message=="Check started")==1,"operation start is logged once");
            var network=events.First(x=>x.Category=="Network");
            Assert(network.Build=="2026.2.257"&&network.Url!=null&&network.OperationId==events.First(x=>x.Message=="Check started").OperationId,"network and UI events share captured context");
            checkedForm.Close();until(()=>checkedForm.IsDisposed);SynchronizationContext.SetSynchronizationContext(null);
        }
        Directory.Delete(dir,true);
    }

}
