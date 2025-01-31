/*
  Sizer.Net
  Written by Bernhard Schelling
  https://github.com/schellingb/sizer-net/

  This is free and unencumbered software released into the public domain.

  Anyone is free to copy, modify, publish, use, compile, sell, or
  distribute this software, either in source code form or as a compiled
  binary, for any purpose, commercial or non-commercial, and by any
  means.

  In jurisdictions that recognize copyright laws, the author or authors
  of this software dedicate any and all copyright interest in the
  software to the public domain. We make this dedication for the benefit
  of the public at large and to the detriment of our heirs and
  successors. We intend this dedication to be an overt act of
  relinquishment in perpetuity of all present and future rights to this
  software under copyright law.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
  IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
  OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
  ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
  OTHER DEALINGS IN THE SOFTWARE.

  For more information, please refer to <http://unlicense.org/>
*/

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class SizerNet
{
    // estimated numbers for byte size of overhead introduced by various things
    private const int OverheadType = 4 + 8 * 2;
    private const int OverheadField = 2 + 2 * 2;
    private const int OverheadMethod = 8 + 6 * 2;
    private const int OverheadLocalVariable = 4 + 1 * 2;
    private const int OverheadParameter = 4 + 1 * 2;
    private const int OverheadInterfaceImpl = 0 + 2 * 2;
    private const int OverheadEvent = 2 + 2 * 2;
    private const int OverheadProperty = 2 + 2 * 2;
    private const int OverheadCustomAttribute = 0 + 3 * 2;
    private static Form _f;
    private static TreeView _tv;
    private static int _treeViewScrollX;
    private static string _assemblyPath;
    private static long _assemblySize;
    private static string[] _dependencyDirs;
    private static string[] _ignoredDependencies;

    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        _f = new Form();
        _f.KeyPreview = true;
        _f.KeyUp += (sender, e) =>
        {
            if (e.KeyCode == Keys.Escape) ((Form)sender).Close();
        };
        _f.Text = "Sizer.Net";
        _f.Icon = Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
        _f.ClientSize = new Size(800, 700);

        _tv = new TreeView();
        _tv.Location = new Point(13, 13);
        _tv.Size = new Size(_f.ClientSize.Width - 13 - 13, _f.ClientSize.Height - 13 - 23 - 13 - 13);
        _tv.DrawMode = TreeViewDrawMode.OwnerDrawText;
        _tv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _tv.Resize += (sender, e) => { _tv.Invalidate(); };
        _tv.DrawNode += OnTreeDrawNode;
        _f.Controls.Add(_tv);

        var btnLoad = new Button();
        btnLoad.Location = new Point(13, _f.ClientSize.Height - 13 - 23);
        btnLoad.Size = new Size(500 - 13, 23);
        btnLoad.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        btnLoad.Text = "Load Assembly";
        btnLoad.Click += (sender, e) => { BrowseAssembly(); };
        _f.Controls.Add(btnLoad);

        var btnClose = new Button();
        btnClose.Location = new Point(513, _f.ClientSize.Height - 13 - 23);
        btnClose.Size = new Size(_f.ClientSize.Width - 513 - 13, 23);
        btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClose.Text = "Close";
        btnClose.Click += (sender, e) => { _f.Close(); };
        _f.Controls.Add(btnClose);

        if (args.Length > 0)
        {
            if (new FileInfo(args[0]).Exists == false)
            {
                MessageBox.Show("Assembly not found: " + args[0], "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            _f.Shown += (sender, e) => { LoadAssembly(args[0], true); };
        }
        else
        {
            _f.Shown += (sender, e) => { BrowseAssembly(true); };
        }

        Application.Run(_f);
    }

    private static void BrowseAssembly(bool initialLoad = false)
    {
        var ofd = new OpenFileDialog();
        ofd.ValidateNames = ofd.CheckFileExists = ofd.CheckPathExists = true;
        ofd.Filter = ".Net Assemblies (*.exe, *.dll)|*.exe;*.dll";
        if (ofd.ShowDialog() != DialogResult.OK)
        {
            if (initialLoad) _f.Close();
            return;
        }

        ofd.Dispose();
        LoadAssembly(ofd.FileName, initialLoad);
    }

    private static void OnTreeDrawNode(object sender, DrawTreeNodeEventArgs e)
    {
        e.DrawDefault = true;
        if (_tv.Nodes.Count == 0 || e.Bounds.Height == 0) return;
        var pct = (float)(long)e.Node.Tag / _assemblySize;
        int w = _tv.ClientSize.Width / 4, x = _tv.ClientSize.Width - w - 5, size = (int)(w * pct);
        e.Graphics.FillRectangle(Brushes.White, x, e.Bounds.Top + 1, w + 1, e.Bounds.Height - 2);
        e.Graphics.FillRectangle(Brushes.LightGray, x, e.Bounds.Top + 1, size + 1, e.Bounds.Height - 2);
        e.Graphics.DrawRectangle(Pens.DarkGray, x, e.Bounds.Top + 1, w + 1, e.Bounds.Height - 2);
        if (true) //ShowInKilobytes
            e.Graphics.DrawString(((long)e.Node.Tag / 1024f).ToString("0.##") + " kb", _tv.Font, Brushes.DarkSlateGray, x, e.Bounds.Top + 1);
        //else (ShowInBytes)
        //  e.Graphics.DrawString(((long)e.Node.Tag).ToString() + " b", tv.Font, Brushes.DarkSlateGray, x, e.Bounds.Top + 1);
        //else (ShowInPercent)
        //  e.Graphics.DrawString((pct*100).ToString("0.##") + "%", tv.Font, Brushes.DarkSlateGray, x, e.Bounds.Top + 1);
        e.Graphics.DrawLine((e.State & TreeNodeStates.Selected) != 0 ? SystemPens.Highlight : SystemPens.ControlLight, e.Bounds.Left + 5, e.Bounds.Top + e.Bounds.Height / 2, x - 5, e.Bounds.Top + e.Bounds.Height / 2);
        if (_tv.Nodes[0].Bounds.X != _treeViewScrollX)
        {
            _treeViewScrollX = _tv.Nodes[0].Bounds.X;
            _tv.Invalidate();
        }
    }

    private static void LoadAssembly(string inAssemblyPath, bool initialLoad = false)
    {
        _assemblyPath = inAssemblyPath;
        try
        {
            _tv.Nodes.Clear();
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveExternalAssembly;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveExternalAssembly;
            _assemblyPath = new FileInfo(_assemblyPath).FullName;
            _dependencyDirs = new[] { Path.GetDirectoryName(_assemblyPath), Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) };
            _ignoredDependencies = [];
            var assembly = Assembly.LoadFile(_assemblyPath);
            var isReflectionOnly = false;
            _assemblySize = new FileInfo(assembly.Location).Length;

            if (_assemblyPath != assembly.Location && !FileContentsMatch(_assemblyPath, assembly.Location))
            {
                MessageBox.Show("Requested assembly:\n" + _assemblyPath + "\n\nAssembly loaded by system:\n" + assembly.Location + "\n\nA different assembly was loaded because an assembly with the same name exists in the global assembly cache.\n\nResorting to loading the assembly in 'reflection only' mode which disables dependency resolving which can make certain type evaluations impossible.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                assembly = Assembly.ReflectionOnlyLoadFrom(_assemblyPath);
                isReflectionOnly = true;
            }

            var all = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var statics = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            var nAssembly = new TreeNode(assembly.GetName().Name);
            nAssembly.Tag = 0L;

            var nResources = nAssembly.Nodes.Add("Resources");
            nResources.Tag = 0L;

            // Enumerate Win32 resources
            try
            {
                var assemblyHandle = LoadLibraryEx(_assemblyPath, IntPtr.Zero, LoadLibraryFlags.LoadLibraryAsDatafile);

                if (assemblyHandle == IntPtr.Zero)
                {
                    throw new Exception();
                }

                EnumResourceTypes(assemblyHandle, (hModule, lpszType, lParam) =>
                {
                    try
                    {
                        lpszType.ToInt32();
                    }
                    catch (Exception)
                    {
                        return true;
                    }

                    EnumResourceNames(hModule, lpszType.ToInt32(), (hModule2, lpszType2, lpzName, lParam2) =>
                    {
                        var rt = unchecked((ResType)lpszType2);
                        var hResource = FindResource(hModule2, lpzName, lpszType2);
                        long size = SizeofResource(hModule2, hResource);

                        var name = Marshal.PtrToStringUni(lpzName);
                        var nResource = nResources.Nodes.Add("Resource: " + rt + " " + (name ?? "#" + lpzName.ToInt64()));
                        SetNodeTag(nResource, size);

                        return true;
                    }, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
                FreeLibrary(assemblyHandle);
            }
            catch (Exception)
            {
                // ignore Win32 resources
            }

            // Enumerate manifest resources
            foreach (var mr in assembly.GetManifestResourceNames())
            {
                var rl = assembly.GetManifestResourceInfo(mr).ResourceLocation;
                if ((rl & ResourceLocation.Embedded) == 0 || (rl & ResourceLocation.ContainedInAnotherAssembly) != 0)
                {
                    continue;
                }

                var nResource = nResources.Nodes.Add("Manifest Resource: " + mr);
                var mrs = assembly.GetManifestResourceStream(mr);
                SetNodeTag(nResource, mrs.Length);
                mrs.Dispose();
            }

            foreach (var module in assembly.GetModules())
            {
                foreach (var mi in module.GetMethods(all))
                {
                    AddMethodNode(nAssembly, mi);
                }

                var lenModuleFields = 0;

                foreach (var fi in module.GetFields(all))
                {
                    lenModuleFields += OverheadField + fi.Name.Length;
                }

                if (lenModuleFields != 0)
                {
                    var nModuleInfo = nAssembly.Nodes.Add(module.GetFields(all).Length + " Fields in " + module.Name + " (Overhead)");
                    SetNodeTag(nModuleInfo, lenModuleFields);
                }
            }

            Type[] assemblyTypes;
            try
            {
                assemblyTypes = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                assemblyTypes = e.Types;
            }

            var unresolvedTypes = 0;
            foreach (var type in assemblyTypes)
            {
                if (type == null)
                {
                    unresolvedTypes++;
                    continue;
                }

                var nType = nAssembly;
                var isStaticArrayInitType = type.Name.Contains("StaticArrayInitTypeSize=");

                foreach (var nsPart in type.FullName.Split('.', '+'))
                {
                    if (nType.Nodes.ContainsKey(nsPart))
                    {
                        nType = nType.Nodes[nsPart];
                    }
                    else
                    {
                        (nType = nType.Nodes.Add(nsPart, nsPart)).Tag = 0L;
                    }
                }

                var lenType = OverheadType + type.FullName.Length;

                try
                {
                    foreach (var it in type.GetInterfaces())
                    {
                        lenType += OverheadInterfaceImpl;
                    }
                }
                catch
                {
                }
#if DOTNET35
                try { foreach (object ca in type.GetCustomAttributes(false)) lenType += Overhead_CustomAttribute; } catch { }
#else
                try
                {
                    foreach (var ad in type.GetCustomAttributesData())
                    {
                        lenType += OverheadCustomAttribute;
                    }
                }
                catch
                {
                }
#endif
                SetNodeTag(nType, lenType);

                foreach (var fi in type.GetFields(statics))
                {
                    try
                    {
                        if (fi.FieldType.ContainsGenericParameters || fi.FieldType.IsGenericType)
                        {
                            continue;
                        }

                        var fiSize = CalculateSize(isReflectionOnly, fi.FieldType, fi);
                        if (fiSize > 0)
                        {
                            SetNodeTag(nType.Nodes.Add("Static Field: " + fi.Name), fiSize);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                int numTypeFields = 0, numTypeProperties = 0, numTypeEvents = 0, lenTypeFields = 0, lenTypeProperties = 0, lenTypeEvents = 0;
                foreach (var fi in type.GetFields(all))
                {
                    numTypeFields++;
                    lenTypeFields += OverheadField + fi.Name.Length;
                }

                foreach (var pi in type.GetProperties(all))
                {
                    numTypeProperties++;
                    lenTypeProperties += OverheadProperty + (pi.Name?.Length ?? 0);
                }

                foreach (var ei in type.GetEvents(all))
                {
                    numTypeEvents++;
                    lenTypeEvents += OverheadEvent + ei.Name.Length;
                }

                if (lenTypeFields != 0)
                {
                    SetNodeTag(nType.Nodes.Add(numTypeFields + " Fields (Overhead)"), lenTypeFields);
                }

                if (lenTypeProperties != 0)
                {
                    SetNodeTag(nType.Nodes.Add(numTypeProperties + " Properties (Overhead)"), lenTypeProperties);
                }

                if (lenTypeEvents != 0)
                {
                    SetNodeTag(nType.Nodes.Add(numTypeEvents + " Events (Overhead)"), lenTypeEvents);
                }

                foreach (var ci in type.GetConstructors(all))
                {
                    AddMethodNode(nType, ci);
                }

                foreach (var mi in type.GetMethods(all))
                {
                    AddMethodNode(nType, mi);
                }
            }

            SetNodeTag(nAssembly.Nodes.Add("Other Overhead"), _assemblySize - (long)nAssembly.Tag);
            SortByNodeByTag(nAssembly.Nodes);
            nAssembly.Expand();
            _tv.Nodes.Add(nAssembly);

            if (unresolvedTypes != 0)
            {
                MessageBox.Show(unresolvedTypes + " types could not be evaluated due to missing dependency errors.\nThese are included in the 'Other Overhead' entry.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception e)
        {
            MessageBox.Show("Assembly loading error:\n\n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            if (initialLoad) _f.Close();
        }
    }

    private static void AddMethodNode(TreeNode parentNode, MethodBase mi)
    {
        var nMethod = parentNode.Nodes.Add(mi.Name);
        var lenMi = OverheadMethod + mi.Name.Length;

        try
        {
            var mb = mi.GetMethodBody();
            lenMi += mb.GetILAsByteArray().Length;

            foreach (var lvi in mb.LocalVariables)
            {
                lenMi += OverheadLocalVariable;
            }
        }
        catch
        {
        }

        try
        {
            foreach (var pi in mi.GetParameters())
            {
                lenMi += 16 + (pi.Name?.Length ?? 0);
            }
        }
        catch
        {
        }
#if DOTNET35
        try { foreach (object ca in mi.GetCustomAttributes(false)) lenMi += Overhead_CustomAttribute; } catch { }
#else
        try
        {
            foreach (var ad in mi.GetCustomAttributesData())
            {
                lenMi += OverheadCustomAttribute;
            }
        }
        catch
        {
        }
#endif
        SetNodeTag(nMethod, lenMi);
    }

    private static Assembly ResolveExternalAssembly(object sender, ResolveEventArgs args)
    {
        if (Array.IndexOf(_ignoredDependencies, args.Name) >= 0)
        {
            return null;
        }

        var dllFileName = new AssemblyName(args.Name).Name + ".dll";
        foreach (var dir in _dependencyDirs)
        {
            var testAssemblyPath = Path.Combine(dir, dllFileName);
            if (File.Exists(testAssemblyPath))
            {
                return Assembly.LoadFile(testAssemblyPath);
            }
        }

        var ofd = new OpenFileDialog();
        ofd.Title = "Find dependency: " + args.Name;
        ofd.ValidateNames = ofd.CheckFileExists = ofd.CheckPathExists = true;
        ofd.FileName = dllFileName;
        ofd.Filter = ".Net Dependencies (*.dll)|*.dll";
        if (ofd.ShowDialog() != DialogResult.OK)
        {
            Array.Resize(ref _ignoredDependencies, _ignoredDependencies.Length + 1);
            _ignoredDependencies[_ignoredDependencies.Length - 1] = args.Name;
            return null;
        }

        Array.Resize(ref _dependencyDirs, _dependencyDirs.Length + 1);
        _dependencyDirs[_dependencyDirs.Length - 1] = Path.GetDirectoryName(ofd.FileName);
        return Assembly.LoadFile(ofd.FileName);
    }

    private static void SetNodeTag(TreeNode n, long amount)
    {
        n.Tag = amount;

        for (n = n.Parent; n != null; n = n.Parent)
        {
            n.Tag = (long)n.Tag + amount;
        }
    }

    private static void SortByNodeByTag(TreeNodeCollection nc)
    {
        foreach (TreeNode n in nc)
        {
            SortByNodeByTag(n.Nodes);
        }

        TreeNode[] ns = new TreeNode[nc.Count];
        nc.CopyTo(ns, 0);
        Array.Sort(ns, (a, b) => { return unchecked((int)((long)b.Tag - (long)a.Tag)); });
        nc.Clear();
        nc.AddRange(ns);
    }

    private static void FilterNodeByTag(TreeNodeCollection nc, long threshold)
    {
        long totalRemoved = 0;
        var lastRemoved = -1;
        for (var i = 0; i != nc.Count; i++)
        {
            if ((long)nc[i].Tag < threshold)
            {
                totalRemoved += (long)nc[i].Tag;
                nc[i].Remove();
                lastRemoved = i--;
                continue;
            }

            FilterNodeByTag(nc[i].Nodes, threshold);
        }

        if (totalRemoved != 0)
        {
            nc.Add("... <Filtered> ...").Tag = totalRemoved;
        }
    }

    private static long CalculateSize(bool isReflectionOnly, Type t, object fiOrValue = null)
    {
        if (t.IsArray)
        {
            var a = (Array)(fiOrValue is FieldInfo ? isReflectionOnly ? ((FieldInfo)fiOrValue).GetRawConstantValue() : ((FieldInfo)fiOrValue).GetValue(null) : fiOrValue);

            if (a == null || a.LongLength == 0)
            {
                return 0;
            }

            t = t.GetElementType();

            if (t.IsEnum)
            {
                t = Enum.GetUnderlyingType(t);
            }

            if (!t.ContainsGenericParameters && !t.IsGenericType && (t.IsValueType || t.IsPointer || t.IsLayoutSequential))
            {
                return a.LongLength * Marshal.SizeOf(t);
            }

            if (!t.IsArray && t != typeof(string))
            {
                return 0; //can't measure size
            }

            long res = 0;

            foreach (var v in a)
            {
                res += CalculateSize(isReflectionOnly, t, v);
            }

            return res;
        }

        if (t == typeof(string))
        {
            var s = (string)(fiOrValue is FieldInfo ? isReflectionOnly ? ((FieldInfo)fiOrValue).GetRawConstantValue() : ((FieldInfo)fiOrValue).GetValue(null) : fiOrValue);
            return s == null ? 0 : s.Length * 2;
        }

        if (t.IsEnum)
        {
            t = Enum.GetUnderlyingType(t);
        }

        return !t.ContainsGenericParameters && !t.IsGenericType && (t.IsValueType || t.IsPointer || t.IsLayoutSequential) ? Marshal.SizeOf(t) : 0;
    }

    private static bool FileContentsMatch(string path1, string path2)
    {
        FileInfo fi1 = new(path1), fi2 = new(path2);

        if (!fi1.Exists || !fi2.Exists || fi1.Length != fi2.Length)
        {
            return false;
        }

        FileStream stream1 = fi1.Open(FileMode.Open, FileAccess.Read, FileShare.Read), stream2 = fi2.Open(FileMode.Open, FileAccess.Read, FileShare.Read);

        for (byte[] buf1 = new byte[4096], buf2 = new byte[4096];;)
        {
            var count = stream1.Read(buf1, 0, 4096);
            stream2.Read(buf2, 0, 4096);

            if (count == 0)
            {
                return true;
            }

            for (var i = 0; i < count; i += sizeof(long))
            {
                if (BitConverter.ToInt64(buf1, i) != BitConverter.ToInt64(buf2, i))
                {
                    return false;
                }
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

    [DllImport("kernel32.dll")]
    private static extern bool EnumResourceTypes(IntPtr hModule, EnumResTypeProc lpEnumFunc, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern bool EnumResourceNames(IntPtr hModule, int dwId, EnumResNameProc lpEnumFunc, IntPtr lParam);

    [DllImport("Kernel32.dll")]
    private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpszName, IntPtr lpszType);

    [DllImport("Kernel32.dll")]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResource);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);

    // PInvoke definitions for Win32 resource enumeration
    private enum LoadLibraryFlags : uint
    {
        LoadLibraryAsDatafile = 2
    }

    private enum ResType
    {
        Cursor = 1,
        Bitmap,
        Icon,
        Menu,
        Dialog,
        String,
        FontDir,
        Font,
        Accelerator,
        RcData,
        MessageTable,
        CursorGroup,
        IconGroup = 14,
        VersionInfo = 16,
        DlgInclude,
        PlugPlay = 19,
        Vxd,
        AnimatedCursor,
        AnimatedIcon,
        Html,
        Manifest
    }

    private delegate bool EnumResTypeProc(IntPtr hModule, IntPtr lpszType, IntPtr lParam);

    private delegate bool EnumResNameProc(IntPtr hModule, IntPtr lpszType, IntPtr lpszName, IntPtr lParam);
}
