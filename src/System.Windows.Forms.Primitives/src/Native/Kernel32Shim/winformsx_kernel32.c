// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#include <stdint.h>
#include <stdlib.h>
#include <string.h>

#if defined(_WIN32)
#define WF_EXPORT __declspec(dllexport)
#else
#define WF_EXPORT __attribute__((visibility("default")))
#endif

typedef int32_t BOOL;
typedef uint32_t DWORD;
typedef uint16_t WCHAR;
typedef uintptr_t SIZE_T;

typedef struct STARTUPINFOW
{
    DWORD cb;
    WCHAR* reserved;
    WCHAR* desktop;
    WCHAR* title;
    DWORD x;
    DWORD y;
    DWORD x_size;
    DWORD y_size;
    DWORD x_count_chars;
    DWORD y_count_chars;
    DWORD fill_attribute;
    DWORD flags;
    uint16_t show_window;
    uint16_t reserved2_count;
    uint8_t* reserved2;
    void* std_input;
    void* std_output;
    void* std_error;
} STARTUPINFOW;

typedef struct ACTCTXW
{
    DWORD cb_size;
    DWORD flags;
    const WCHAR* source;
    uint16_t processor_architecture;
    uint16_t language_id;
    const WCHAR* assembly_directory;
    const WCHAR* resource_name;
    const WCHAR* application_name;
    void* module;
} ACTCTXW;

typedef struct FILETIME
{
    DWORD dw_low_date_time;
    DWORD dw_high_date_time;
} FILETIME;

typedef struct SYSTEMTIME
{
    uint16_t w_year;
    uint16_t w_month;
    uint16_t w_day_of_week;
    uint16_t w_day;
    uint16_t w_hour;
    uint16_t w_minute;
    uint16_t w_second;
    uint16_t w_milliseconds;
} SYSTEMTIME;

typedef struct WinFormsXKernel32Dispatch
{
    uint32_t version;
    uint32_t size;
    void* (*get_current_process)(void);
    DWORD (*get_current_process_id)(void);
    DWORD (*get_current_thread_id)(void);
    void* (*get_module_handle)(const WCHAR* module_name);
    DWORD (*get_module_file_name)(void* module, WCHAR* filename, DWORD size);
    void* (*load_library_ex)(const WCHAR* file_name, void* file, DWORD flags);
    BOOL (*free_library)(void* module);
    void* (*get_proc_address)(void* module, const char* proc_name);
    void* (*find_resource)(void* module, const WCHAR* name, const WCHAR* type);
    void* (*find_resource_ex)(void* module, const WCHAR* type, const WCHAR* name, uint16_t language);
    void* (*load_resource)(void* module, void* resource_info);
    void* (*lock_resource)(void* resource_data);
    DWORD (*sizeof_resource)(void* module, void* resource_info);
    BOOL (*free_resource)(void* resource_data);
    DWORD (*get_last_error)(void);
    void (*set_last_error)(DWORD error);
    BOOL (*close_handle)(void* handle);
    BOOL (*duplicate_handle)(void* source_process, void* source_handle, void* target_process, void** target_handle, DWORD desired_access, BOOL inherit_handle, DWORD options);
    DWORD (*format_message)(DWORD flags, void* source, DWORD message_id, DWORD language_id, WCHAR* buffer, DWORD size, void* arguments);
    BOOL (*get_exit_code_thread)(void* thread, DWORD* exit_code);
    int32_t (*get_locale_info_ex)(const WCHAR* locale_name, DWORD locale_type, WCHAR* data, int32_t data_length);
    void (*get_startup_info)(STARTUPINFOW* startup_info);
    DWORD (*get_thread_locale)(void);
    DWORD (*get_tick_count)(void);
    void* (*global_alloc)(DWORD flags, SIZE_T size);
    void* (*global_realloc)(void* handle, SIZE_T size, DWORD flags);
    void* (*global_lock)(void* handle);
    BOOL (*global_unlock)(void* handle);
    SIZE_T (*global_size)(void* handle);
    void* (*global_free)(void* handle);
    void* (*local_alloc)(DWORD flags, SIZE_T size);
    void* (*local_realloc)(void* handle, SIZE_T size, DWORD flags);
    void* (*local_lock)(void* handle);
    BOOL (*local_unlock)(void* handle);
    SIZE_T (*local_size)(void* handle);
    void* (*local_free)(void* handle);
    void* (*create_actctx)(const ACTCTXW* actctx);
    BOOL (*activate_actctx)(void* actctx, SIZE_T* cookie);
    BOOL (*deactivate_actctx)(DWORD flags, SIZE_T cookie);
    BOOL (*get_current_actctx)(void** actctx);
} WinFormsXKernel32Dispatch;

static WinFormsXKernel32Dispatch g_dispatch;
static DWORD g_last_error;
static uint64_t g_tick_count;
static uint64_t g_performance_counter;
static uint64_t g_system_time_filetime = 132223104000000000ull;
static intptr_t g_next_module_handle = 0x710000;
static intptr_t g_next_activation_context = 0x610000;
static void* g_current_activation_context;
static const int64_t g_local_time_offset_ticks = -(4ll * 60ll * 60ll * 10000000ll);

static int is_leap_year(int year)
{
    return (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
}

static int days_in_month(int year, int month)
{
    static const int month_lengths[12] =
    {
        31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31
    };

    if (month < 1 || month > 12)
    {
        return 0;
    }

    if (month == 2 && is_leap_year(year))
    {
        return 29;
    }

    return month_lengths[month - 1];
}

static uint64_t combine_file_time(const FILETIME* file_time)
{
    return ((uint64_t)file_time->dw_high_date_time << 32) | file_time->dw_low_date_time;
}

static FILETIME split_file_time(uint64_t value)
{
    FILETIME file_time;
    file_time.dw_low_date_time = (DWORD)(value & 0xffffffffu);
    file_time.dw_high_date_time = (DWORD)(value >> 32);
    return file_time;
}

static BOOL system_time_to_file_time_value(const SYSTEMTIME* system_time, uint64_t* value)
{
    if (system_time == 0 || value == 0)
    {
        return 0;
    }

    if (system_time->w_year < 1601
        || system_time->w_month < 1
        || system_time->w_month > 12
        || system_time->w_hour > 23
        || system_time->w_minute > 59
        || system_time->w_second > 59
        || system_time->w_milliseconds > 999)
    {
        return 0;
    }

    int month_days = days_in_month((int)system_time->w_year, (int)system_time->w_month);
    if (system_time->w_day < 1 || system_time->w_day > month_days)
    {
        return 0;
    }

    uint64_t days = 0;
    for (int year = 1601; year < (int)system_time->w_year; year++)
    {
        days += is_leap_year(year) ? 366u : 365u;
    }

    for (int month = 1; month < (int)system_time->w_month; month++)
    {
        days += (uint64_t)days_in_month((int)system_time->w_year, month);
    }

    days += (uint64_t)(system_time->w_day - 1);
    uint64_t seconds =
        days * 86400u
        + (uint64_t)system_time->w_hour * 3600u
        + (uint64_t)system_time->w_minute * 60u
        + (uint64_t)system_time->w_second;

    *value = seconds * 10000000u + (uint64_t)system_time->w_milliseconds * 10000u;
    return 1;
}

static BOOL file_time_value_to_system_time(uint64_t value, SYSTEMTIME* system_time)
{
    if (system_time == 0)
    {
        return 0;
    }

    uint64_t total_days = value / 864000000000ull;
    uint64_t day_remainder = value % 864000000000ull;
    int year = 1601;
    while (1)
    {
        int year_days = is_leap_year(year) ? 366 : 365;
        if (total_days < (uint64_t)year_days)
        {
            break;
        }

        total_days -= (uint64_t)year_days;
        year++;
    }

    int month = 1;
    while (1)
    {
        int month_len = days_in_month(year, month);
        if (total_days < (uint64_t)month_len)
        {
            break;
        }

        total_days -= (uint64_t)month_len;
        month++;
    }

    system_time->w_year = (uint16_t)year;
    system_time->w_month = (uint16_t)month;
    system_time->w_day = (uint16_t)(total_days + 1);
    system_time->w_day_of_week = (uint16_t)((((value / 864000000000ull) + 1ull) % 7ull));
    system_time->w_hour = (uint16_t)(day_remainder / 36000000000ull);
    day_remainder %= 36000000000ull;
    system_time->w_minute = (uint16_t)(day_remainder / 600000000ull);
    day_remainder %= 600000000ull;
    system_time->w_second = (uint16_t)(day_remainder / 10000000ull);
    day_remainder %= 10000000ull;
    system_time->w_milliseconds = (uint16_t)(day_remainder / 10000ull);
    return 1;
}

static uint64_t next_system_time_filetime(void)
{
    g_system_time_filetime += 100000ull;
    return g_system_time_filetime;
}

static void* fallback_memory_base(void* handle)
{
    if (handle == 0)
    {
        return 0;
    }

    return (void*)((uint8_t*)handle - sizeof(SIZE_T));
}

static void* fallback_memory_alloc(DWORD flags, SIZE_T size)
{
    if (size == 0)
    {
        return 0;
    }

    uint8_t* base = (uint8_t*)malloc(sizeof(SIZE_T) + size);
    if (base == 0)
    {
        return 0;
    }

    *((SIZE_T*)base) = size;
    void* handle = base + sizeof(SIZE_T);
    if ((flags & 0x40u) != 0)
    {
        memset(handle, 0, size);
    }

    return handle;
}

static void* fallback_memory_realloc(void* handle, SIZE_T size, DWORD flags)
{
    if (handle == 0)
    {
        return fallback_memory_alloc(flags, size);
    }

    if (size == 0)
    {
        free(fallback_memory_base(handle));
        return 0;
    }

    void* old_base = fallback_memory_base(handle);
    SIZE_T old_size = *((SIZE_T*)old_base);
    uint8_t* new_base = (uint8_t*)realloc(old_base, sizeof(SIZE_T) + size);
    if (new_base == 0)
    {
        return 0;
    }

    *((SIZE_T*)new_base) = size;
    void* new_handle = new_base + sizeof(SIZE_T);
    if ((flags & 0x40u) != 0 && size > old_size)
    {
        memset((uint8_t*)new_handle + old_size, 0, size - old_size);
    }

    return new_handle;
}

static SIZE_T fallback_memory_size(void* handle)
{
    if (handle == 0)
    {
        return 0;
    }

    return *((SIZE_T*)fallback_memory_base(handle));
}

static void* fallback_memory_free(void* handle)
{
    if (handle != 0)
    {
        free(fallback_memory_base(handle));
    }

    return 0;
}

static DWORD copy_ascii_path(char* buffer, DWORD size)
{
    const char* fallback = "dotnet";
    size_t length = strlen(fallback);
    if (buffer == 0 || size == 0)
    {
        return 0;
    }

    size_t copy_length = length < size ? length : size;
    memcpy(buffer, fallback, copy_length);
    if (copy_length < size)
    {
        buffer[copy_length] = '\0';
    }

    return (DWORD)copy_length;
}

WF_EXPORT BOOL WinFormsXKernel32RegisterDispatch(const WinFormsXKernel32Dispatch* dispatch)
{
    if (dispatch == 0 || dispatch->version != 1 || dispatch->size < sizeof(WinFormsXKernel32Dispatch))
    {
        memset(&g_dispatch, 0, sizeof(g_dispatch));
        return 0;
    }

    g_dispatch = *dispatch;
    return 1;
}

WF_EXPORT void* GetCurrentProcess(void)
{
    if (g_dispatch.get_current_process != 0)
    {
        return g_dispatch.get_current_process();
    }

    return (void*)(intptr_t)-1;
}

WF_EXPORT void* GetCurrentThread(void)
{
    return (void*)(intptr_t)-2;
}

WF_EXPORT DWORD GetCurrentProcessId(void)
{
    if (g_dispatch.get_current_process_id != 0)
    {
        return g_dispatch.get_current_process_id();
    }

    return 1;
}

WF_EXPORT DWORD GetCurrentThreadId(void)
{
    if (g_dispatch.get_current_thread_id != 0)
    {
        return g_dispatch.get_current_thread_id();
    }

    return 1;
}

WF_EXPORT void* GetModuleHandleW(const WCHAR* module_name)
{
    if (g_dispatch.get_module_handle != 0)
    {
        return g_dispatch.get_module_handle(module_name);
    }

    return (void*)(intptr_t)0x400000;
}

WF_EXPORT void* GetModuleHandleA(const char* module_name)
{
    (void)module_name;
    return GetModuleHandleW(0);
}

WF_EXPORT void* GetModuleHandle(const WCHAR* module_name)
{
    return GetModuleHandleW(module_name);
}

WF_EXPORT DWORD GetModuleFileNameW(void* module, WCHAR* filename, DWORD size)
{
    if (g_dispatch.get_module_file_name != 0)
    {
        return g_dispatch.get_module_file_name(module, filename, size);
    }

    if (filename == 0 || size == 0)
    {
        return 0;
    }

    const WCHAR fallback[] = { 'd', 'o', 't', 'n', 'e', 't', 0 };
    DWORD length = 6;
    DWORD copy_length = length < size ? length : size;
    for (DWORD i = 0; i < copy_length; i++)
    {
        filename[i] = fallback[i];
    }

    if (copy_length < size)
    {
        filename[copy_length] = 0;
    }

    return copy_length;
}

WF_EXPORT DWORD GetModuleFileNameA(void* module, char* filename, DWORD size)
{
    (void)module;
    return copy_ascii_path(filename, size);
}

WF_EXPORT DWORD GetModuleFileName(void* module, WCHAR* filename, DWORD size)
{
    return GetModuleFileNameW(module, filename, size);
}

WF_EXPORT void* LoadLibraryExW(const WCHAR* file_name, void* file, DWORD flags)
{
    if (g_dispatch.load_library_ex != 0)
    {
        return g_dispatch.load_library_ex(file_name, file, flags);
    }

    (void)file;
    (void)flags;
    if (file_name == 0)
    {
        return 0;
    }

    g_next_module_handle++;
    return (void*)g_next_module_handle;
}

WF_EXPORT void* LoadLibraryW(const WCHAR* file_name)
{
    return LoadLibraryExW(file_name, 0, 0);
}

WF_EXPORT void* LoadLibraryExA(const char* file_name, void* file, DWORD flags)
{
    if (file_name == 0)
    {
        return 0;
    }

    (void)file;
    (void)flags;
    g_next_module_handle++;
    return (void*)g_next_module_handle;
}

WF_EXPORT void* LoadLibraryA(const char* file_name)
{
    return LoadLibraryExA(file_name, 0, 0);
}

WF_EXPORT void* LoadLibrary(const WCHAR* file_name)
{
    return LoadLibraryW(file_name);
}

WF_EXPORT BOOL FreeLibrary(void* module)
{
    if (g_dispatch.free_library != 0)
    {
        return g_dispatch.free_library(module);
    }

    return module != 0;
}

WF_EXPORT void* GetProcAddress(void* module, const char* proc_name)
{
    if (g_dispatch.get_proc_address != 0)
    {
        return g_dispatch.get_proc_address(module, proc_name);
    }

    (void)module;
    (void)proc_name;
    return 0;
}

WF_EXPORT void* FindResourceW(void* module, const WCHAR* name, const WCHAR* type)
{
    if (g_dispatch.find_resource != 0)
    {
        return g_dispatch.find_resource(module, name, type);
    }

    (void)module;
    (void)name;
    (void)type;
    g_last_error = 1814;
    return 0;
}

WF_EXPORT void* FindResourceA(void* module, const char* name, const char* type)
{
    (void)name;
    (void)type;
    return FindResourceW(module, 0, 0);
}

WF_EXPORT void* FindResource(void* module, const WCHAR* name, const WCHAR* type)
{
    return FindResourceW(module, name, type);
}

WF_EXPORT void* FindResourceExW(void* module, const WCHAR* type, const WCHAR* name, uint16_t language)
{
    if (g_dispatch.find_resource_ex != 0)
    {
        return g_dispatch.find_resource_ex(module, type, name, language);
    }

    (void)module;
    (void)type;
    (void)name;
    (void)language;
    g_last_error = 1814;
    return 0;
}

WF_EXPORT void* FindResourceExA(void* module, const char* type, const char* name, uint16_t language)
{
    (void)type;
    (void)name;
    return FindResourceExW(module, 0, 0, language);
}

WF_EXPORT void* LoadResource(void* module, void* resource_info)
{
    if (g_dispatch.load_resource != 0)
    {
        return g_dispatch.load_resource(module, resource_info);
    }

    (void)module;
    (void)resource_info;
    g_last_error = 1814;
    return 0;
}

WF_EXPORT void* LockResource(void* resource_data)
{
    if (g_dispatch.lock_resource != 0)
    {
        return g_dispatch.lock_resource(resource_data);
    }

    (void)resource_data;
    g_last_error = 1814;
    return 0;
}

WF_EXPORT DWORD SizeofResource(void* module, void* resource_info)
{
    if (g_dispatch.sizeof_resource != 0)
    {
        return g_dispatch.sizeof_resource(module, resource_info);
    }

    (void)module;
    (void)resource_info;
    g_last_error = 1814;
    return 0;
}

WF_EXPORT BOOL FreeResource(void* resource_data)
{
    if (g_dispatch.free_resource != 0)
    {
        return g_dispatch.free_resource(resource_data);
    }

    (void)resource_data;
    return 0;
}

WF_EXPORT DWORD GetLastError(void)
{
    if (g_dispatch.get_last_error != 0)
    {
        return g_dispatch.get_last_error();
    }

    return g_last_error;
}

WF_EXPORT void SetLastError(DWORD error)
{
    g_last_error = error;
    if (g_dispatch.set_last_error != 0)
    {
        g_dispatch.set_last_error(error);
    }
}

WF_EXPORT BOOL CloseHandle(void* handle)
{
    if (g_dispatch.close_handle != 0)
    {
        return g_dispatch.close_handle(handle);
    }

    (void)handle;
    return 1;
}

WF_EXPORT BOOL DuplicateHandle(void* source_process, void* source_handle, void* target_process, void** target_handle, DWORD desired_access, BOOL inherit_handle, DWORD options)
{
    if (g_dispatch.duplicate_handle != 0)
    {
        return g_dispatch.duplicate_handle(source_process, source_handle, target_process, target_handle, desired_access, inherit_handle, options);
    }

    (void)source_process;
    (void)target_process;
    (void)desired_access;
    (void)inherit_handle;
    (void)options;
    if (target_handle == 0)
    {
        return 0;
    }

    *target_handle = source_handle;
    return 1;
}

WF_EXPORT DWORD FormatMessageW(DWORD flags, void* source, DWORD message_id, DWORD language_id, WCHAR* buffer, DWORD size, void* arguments)
{
    if (g_dispatch.format_message != 0)
    {
        return g_dispatch.format_message(flags, source, message_id, language_id, buffer, size, arguments);
    }

    (void)flags;
    (void)source;
    (void)message_id;
    (void)language_id;
    (void)arguments;
    if (buffer == 0 || size == 0)
    {
        return 0;
    }

    const WCHAR fallback[] = { 'W', 'i', 'n', 'F', 'o', 'r', 'm', 's', 'X', ' ', 's', 'y', 's', 't', 'e', 'm', ' ', 'm', 'e', 's', 's', 'a', 'g', 'e', '.', 0 };
    DWORD length = 25;
    DWORD copy_length = length < size ? length : size - 1;
    for (DWORD i = 0; i < copy_length; i++)
    {
        buffer[i] = fallback[i];
    }

    buffer[copy_length] = 0;
    return copy_length;
}

WF_EXPORT DWORD FormatMessage(DWORD flags, void* source, DWORD message_id, DWORD language_id, WCHAR* buffer, DWORD size, void* arguments)
{
    return FormatMessageW(flags, source, message_id, language_id, buffer, size, arguments);
}

WF_EXPORT BOOL GetExitCodeThread(void* thread, DWORD* exit_code)
{
    if (g_dispatch.get_exit_code_thread != 0)
    {
        return g_dispatch.get_exit_code_thread(thread, exit_code);
    }

    (void)thread;
    if (exit_code == 0)
    {
        return 0;
    }

    *exit_code = 259;
    return 1;
}

WF_EXPORT int32_t GetLocaleInfoEx(const WCHAR* locale_name, DWORD locale_type, WCHAR* data, int32_t data_length)
{
    if (g_dispatch.get_locale_info_ex != 0)
    {
        return g_dispatch.get_locale_info_ex(locale_name, locale_type, data, data_length);
    }

    (void)locale_name;
    if (data == 0 || data_length <= 0)
    {
        return 0;
    }

    if (locale_type == 0x0000000D)
    {
        data[0] = '1';
        if (data_length > 1)
        {
            data[1] = 0;
        }

        return 2;
    }

    data[0] = 0;
    return 0;
}

WF_EXPORT void GetStartupInfoW(STARTUPINFOW* startup_info)
{
    if (g_dispatch.get_startup_info != 0)
    {
        g_dispatch.get_startup_info(startup_info);
        return;
    }

    if (startup_info == 0)
    {
        return;
    }

    memset(startup_info, 0, sizeof(STARTUPINFOW));
    startup_info->cb = sizeof(STARTUPINFOW);
}

WF_EXPORT void GetStartupInfo(STARTUPINFOW* startup_info)
{
    GetStartupInfoW(startup_info);
}

WF_EXPORT DWORD GetThreadLocale(void)
{
    if (g_dispatch.get_thread_locale != 0)
    {
        return g_dispatch.get_thread_locale();
    }

    return 0x0409;
}

WF_EXPORT DWORD GetTickCount(void)
{
    if (g_dispatch.get_tick_count != 0)
    {
        return g_dispatch.get_tick_count();
    }

    g_tick_count += 16;
    return (DWORD)g_tick_count;
}

WF_EXPORT void GetSystemTimeAsFileTime(FILETIME* file_time)
{
    if (file_time == 0)
    {
        return;
    }

    *file_time = split_file_time(next_system_time_filetime());
}

WF_EXPORT void GetSystemTime(SYSTEMTIME* system_time)
{
    if (system_time == 0)
    {
        return;
    }

    if (!file_time_value_to_system_time(next_system_time_filetime(), system_time))
    {
        memset(system_time, 0, sizeof(SYSTEMTIME));
    }
}

WF_EXPORT void GetLocalTime(SYSTEMTIME* system_time)
{
    if (system_time == 0)
    {
        return;
    }

    int64_t local_value = (int64_t)next_system_time_filetime() + g_local_time_offset_ticks;
    if (local_value < 0)
    {
        local_value = 0;
    }

    if (!file_time_value_to_system_time((uint64_t)local_value, system_time))
    {
        memset(system_time, 0, sizeof(SYSTEMTIME));
    }
}

WF_EXPORT BOOL FileTimeToSystemTime(const FILETIME* file_time, SYSTEMTIME* system_time)
{
    if (file_time == 0 || system_time == 0)
    {
        return 0;
    }

    return file_time_value_to_system_time(combine_file_time(file_time), system_time);
}

WF_EXPORT BOOL SystemTimeToFileTime(const SYSTEMTIME* system_time, FILETIME* file_time)
{
    uint64_t value;
    if (file_time == 0 || !system_time_to_file_time_value(system_time, &value))
    {
        return 0;
    }

    *file_time = split_file_time(value);
    return 1;
}

WF_EXPORT uint64_t GetTickCount64(void)
{
    if (g_dispatch.get_tick_count != 0)
    {
        return (uint64_t)g_dispatch.get_tick_count();
    }

    g_tick_count += 16;
    return g_tick_count;
}

WF_EXPORT BOOL QueryPerformanceFrequency(int64_t* frequency)
{
    if (frequency == 0)
    {
        return 0;
    }

    *frequency = 10000000;
    return 1;
}

WF_EXPORT BOOL QueryPerformanceCounter(int64_t* counter)
{
    if (counter == 0)
    {
        return 0;
    }

    g_performance_counter += 166667;
    *counter = (int64_t)g_performance_counter;
    return 1;
}

WF_EXPORT DWORD GetACP(void)
{
    return 1252;
}

WF_EXPORT DWORD GetOEMCP(void)
{
    return 437;
}

WF_EXPORT DWORD GetSystemDefaultLCID(void)
{
    return 0x0409;
}

WF_EXPORT DWORD GetUserDefaultLCID(void)
{
    return 0x0409;
}

WF_EXPORT void* GlobalAlloc(DWORD flags, SIZE_T size)
{
    if (g_dispatch.global_alloc != 0)
    {
        return g_dispatch.global_alloc(flags, size);
    }

    return fallback_memory_alloc(flags, size);
}

WF_EXPORT void* GlobalReAlloc(void* handle, SIZE_T size, DWORD flags)
{
    if (g_dispatch.global_realloc != 0)
    {
        return g_dispatch.global_realloc(handle, size, flags);
    }

    return fallback_memory_realloc(handle, size, flags);
}

WF_EXPORT void* GlobalLock(void* handle)
{
    if (g_dispatch.global_lock != 0)
    {
        return g_dispatch.global_lock(handle);
    }

    return handle;
}

WF_EXPORT BOOL GlobalUnlock(void* handle)
{
    if (g_dispatch.global_unlock != 0)
    {
        return g_dispatch.global_unlock(handle);
    }

    (void)handle;
    return 0;
}

WF_EXPORT SIZE_T GlobalSize(void* handle)
{
    if (g_dispatch.global_size != 0)
    {
        return g_dispatch.global_size(handle);
    }

    return fallback_memory_size(handle);
}

WF_EXPORT void* GlobalFree(void* handle)
{
    if (g_dispatch.global_free != 0)
    {
        return g_dispatch.global_free(handle);
    }

    return fallback_memory_free(handle);
}

WF_EXPORT void* LocalAlloc(DWORD flags, SIZE_T size)
{
    if (g_dispatch.local_alloc != 0)
    {
        return g_dispatch.local_alloc(flags, size);
    }

    return fallback_memory_alloc(flags, size);
}

WF_EXPORT void* LocalReAlloc(void* handle, SIZE_T size, DWORD flags)
{
    if (g_dispatch.local_realloc != 0)
    {
        return g_dispatch.local_realloc(handle, size, flags);
    }

    return fallback_memory_realloc(handle, size, flags);
}

WF_EXPORT void* LocalLock(void* handle)
{
    if (g_dispatch.local_lock != 0)
    {
        return g_dispatch.local_lock(handle);
    }

    return handle;
}

WF_EXPORT BOOL LocalUnlock(void* handle)
{
    if (g_dispatch.local_unlock != 0)
    {
        return g_dispatch.local_unlock(handle);
    }

    (void)handle;
    return 0;
}

WF_EXPORT SIZE_T LocalSize(void* handle)
{
    if (g_dispatch.local_size != 0)
    {
        return g_dispatch.local_size(handle);
    }

    return fallback_memory_size(handle);
}

WF_EXPORT void* LocalFree(void* handle)
{
    if (g_dispatch.local_free != 0)
    {
        return g_dispatch.local_free(handle);
    }

    return fallback_memory_free(handle);
}

WF_EXPORT void* CreateActCtxW(const ACTCTXW* actctx)
{
    if (g_dispatch.create_actctx != 0)
    {
        return g_dispatch.create_actctx(actctx);
    }

    if (actctx == 0 || actctx->cb_size < sizeof(ACTCTXW))
    {
        return (void*)(intptr_t)-1;
    }

    g_next_activation_context++;
    return (void*)g_next_activation_context;
}

WF_EXPORT void* CreateActCtx(const ACTCTXW* actctx)
{
    return CreateActCtxW(actctx);
}

WF_EXPORT BOOL ActivateActCtx(void* actctx, SIZE_T* cookie)
{
    if (g_dispatch.activate_actctx != 0)
    {
        return g_dispatch.activate_actctx(actctx, cookie);
    }

    if (actctx == 0 || actctx == (void*)(intptr_t)-1 || cookie == 0)
    {
        return 0;
    }

    g_current_activation_context = actctx;
    *cookie = (SIZE_T)actctx;
    return 1;
}

WF_EXPORT BOOL DeactivateActCtx(DWORD flags, SIZE_T cookie)
{
    if (g_dispatch.deactivate_actctx != 0)
    {
        return g_dispatch.deactivate_actctx(flags, cookie);
    }

    (void)flags;
    if (cookie == 0)
    {
        return 0;
    }

    g_current_activation_context = 0;
    return 1;
}

WF_EXPORT BOOL GetCurrentActCtx(void** actctx)
{
    if (g_dispatch.get_current_actctx != 0)
    {
        return g_dispatch.get_current_actctx(actctx);
    }

    if (actctx == 0)
    {
        return 0;
    }

    *actctx = g_current_activation_context;
    return g_current_activation_context != 0;
}
