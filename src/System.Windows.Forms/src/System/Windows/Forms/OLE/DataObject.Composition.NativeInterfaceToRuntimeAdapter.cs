// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using Com = Windows.Win32.System.Com;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace System.Windows.Forms;

public unsafe partial class DataObject
{
    internal unsafe partial class Composition
    {
        /// <summary>
        ///  Maps <see cref="Com.IDataObject.Interface"/> to <see cref="ComTypes.IDataObject"/> without relying on COM GIT.
        /// </summary>
        private sealed class NativeInterfaceToRuntimeAdapter : ComTypes.IDataObject
        {
            private readonly Com.IDataObject.Interface _nativeDataObject;

            public NativeInterfaceToRuntimeAdapter(Com.IDataObject.Interface nativeDataObject)
            {
                _nativeDataObject = nativeDataObject;
            }

            int ComTypes.IDataObject.DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection)
            {
                using var nativeAdviseSink = ComHelpers.TryGetComScope<Com.IAdviseSink>(adviseSink);
                fixed (Com.FORMATETC* nativeFormat = &Unsafe.As<FORMATETC, Com.FORMATETC>(ref pFormatetc))
                fixed (int* pConnection = &connection)
                {
                    return _nativeDataObject.DAdvise(nativeFormat, (uint)advf, nativeAdviseSink, (uint*)pConnection);
                }
            }

            void ComTypes.IDataObject.DUnadvise(int connection)
            {
                _nativeDataObject.DUnadvise((uint)connection).ThrowOnFailure();
            }

            int ComTypes.IDataObject.EnumDAdvise(out IEnumSTATDATA? enumAdvise)
            {
                using ComScope<Com.IEnumSTATDATA> nativeStatData = new(null);
                HRESULT result = _nativeDataObject.EnumDAdvise(nativeStatData);
                ComHelpers.TryGetObjectForIUnknown(nativeStatData.AsUnknown, out enumAdvise);
                return result;
            }

            IEnumFORMATETC ComTypes.IDataObject.EnumFormatEtc(DATADIR direction)
            {
                using ComScope<Com.IEnumFORMATETC> nativeFormat = new(null);
                if (_nativeDataObject.EnumFormatEtc((uint)direction, nativeFormat).Failed)
                {
                    throw new NotSupportedException(SR.ExternalException);
                }

                return (IEnumFORMATETC)ComHelpers.GetObjectForIUnknown(nativeFormat);
            }

            int ComTypes.IDataObject.GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
            {
                HRESULT result = _nativeDataObject.GetCanonicalFormatEtc(Unsafe.As<FORMATETC, Com.FORMATETC>(ref formatIn), out Com.FORMATETC nativeFormat);
                formatOut = Unsafe.As<Com.FORMATETC, FORMATETC>(ref nativeFormat);
                return result;
            }

            void ComTypes.IDataObject.GetData(ref FORMATETC format, out STGMEDIUM medium)
            {
                Com.FORMATETC nativeFormat = Unsafe.As<FORMATETC, Com.FORMATETC>(ref format);
                Com.STGMEDIUM nativeMedium = default;
                _nativeDataObject.GetData(&nativeFormat, &nativeMedium).ThrowOnFailure();
                medium = (STGMEDIUM)nativeMedium;
                nativeMedium.ReleaseUnknown();
            }

            void ComTypes.IDataObject.GetDataHere(ref FORMATETC format, ref STGMEDIUM medium)
            {
                Com.FORMATETC nativeFormat = Unsafe.As<FORMATETC, Com.FORMATETC>(ref format);
                Com.STGMEDIUM nativeMedium = (Com.STGMEDIUM)medium;
                _nativeDataObject.GetDataHere(&nativeFormat, &nativeMedium).ThrowOnFailure();
                medium = (STGMEDIUM)nativeMedium;
                nativeMedium.ReleaseUnknown();
            }

            int ComTypes.IDataObject.QueryGetData(ref FORMATETC format)
            {
                return _nativeDataObject.QueryGetData(Unsafe.As<FORMATETC, Com.FORMATETC>(ref format));
            }

            void ComTypes.IDataObject.SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release)
            {
                Com.STGMEDIUM nativeMedium = (Com.STGMEDIUM)medium;
                Com.FORMATETC nativeFormat = Unsafe.As<FORMATETC, Com.FORMATETC>(ref formatIn);
                HRESULT result = _nativeDataObject.SetData(&nativeFormat, &nativeMedium, release);
                medium = (STGMEDIUM)nativeMedium;
                nativeMedium.ReleaseUnknown();
                result.ThrowOnFailure();
            }
        }
    }
}
