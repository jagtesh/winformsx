// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Windows.Forms.Platform;

/// <summary>
/// Message pump and inter-window communication abstraction.
/// </summary>
internal unsafe interface IMessageInterop
{
    // ─── Send / Post ────────────────────────────────────────────────────

    LRESULT SendMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);
    bool PostMessage(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam);
    LRESULT SendMessage(HWND hWnd, MessageId msg, WPARAM wParam, LPARAM lParam);

    // ─── Message Loop ───────────────────────────────────────────────────

    bool PeekMessage(out MSG lpMsg, HWND hWnd, uint wMsgFilterMin, uint wMsgFilterMax, PEEK_MESSAGE_REMOVE_TYPE wRemoveMsg);
    bool GetMessage(out MSG lpMsg, HWND hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    bool TranslateMessage(in MSG lpMsg);
    LRESULT DispatchMessage(in MSG lpMsg);
    uint MsgWaitForMultipleObjectsEx(
        uint nCount,
        HANDLE* pHandles,
        uint dwMilliseconds,
        QUEUE_STATUS_FLAGS dwWakeMask,
        MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS dwFlags);

    // ─── Timed / Async ──────────────────────────────────────────────────

    bool SendMessageTimeout(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam,
        SEND_MESSAGE_TIMEOUT_FLAGS flags, uint timeout, nuint* pdwResult);
    bool SendMessageCallback(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, Action callback);

    // ─── Registration ───────────────────────────────────────────────────

    uint RegisterWindowMessage(string lpString);
}
