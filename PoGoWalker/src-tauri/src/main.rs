// Prevents an extra console window on Windows in release.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use tauri_plugin_shell::process::CommandEvent;
use tauri_plugin_shell::ShellExt;

// Startup sequence:
//   1. Fire ONE elevated tunnel (`pymobiledevice3 lockdown start-tunnel`) — the
//      single UAC prompt. On Windows this is launched via `runas`. The tunnel
//      stays alive for the session and prints the RSD host/port.
//   2. Launch the bundled, NON-privileged pogo-service sidecar, passing the RSD
//      endpoint through the environment. The UI then talks to it over
//      localhost:8723.
//
// For non-Windows dev (no usbmux), step 1 is skipped and the service falls back
// to its mock device, so the UI is still fully exercisable.

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            let handle = app.handle().clone();

            #[cfg(target_os = "windows")]
            start_elevated_tunnel();

            // Launch the FastAPI sidecar (normal privilege).
            let sidecar = app
                .shell()
                .sidecar("pogo-service")
                .expect("pogo-service sidecar missing");
            let (mut rx, _child) = sidecar.spawn().expect("failed to start pogo-service");

            tauri::async_runtime::spawn(async move {
                while let Some(event) = rx.recv().await {
                    if let CommandEvent::Stderr(line) | CommandEvent::Stdout(line) = event {
                        println!("[service] {}", String::from_utf8_lossy(&line));
                    }
                }
                let _ = handle; // keep handle alive for future IPC
            });

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running PoGo Walker");
}

#[cfg(target_os = "windows")]
fn start_elevated_tunnel() {
    use std::os::windows::process::CommandExt;
    use std::process::Command;

    // `runas` triggers the single UAC elevation prompt. PowerShell keeps the
    // elevated tunnel process alive in its own window.
    const CREATE_NO_WINDOW: u32 = 0x0800_0000;
    let _ = Command::new("powershell")
        .args([
            "-Command",
            "Start-Process",
            "-Verb",
            "RunAs",
            "python",
            "-ArgumentList",
            "'-m','pymobiledevice3','lockdown','start-tunnel'",
        ])
        .creation_flags(CREATE_NO_WINDOW)
        .spawn();
}
