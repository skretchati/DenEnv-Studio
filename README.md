DevEnv Studio

Encrypted, integrity-verified backups of your entire development environment.

DevEnv Studio is a Windows desktop application (WPF / .NET 8) that snapshots your IDE settings, package managers, SDKs, SSH keys, Git configuration, environment variables, certificates, WSL distributions, and more — then bundles everything into a portable, encrypted .devbackup archive with AES-256-GCM, tamper-evident HMAC signatures, and optional password-based double-wrapping.

Features — Backup
VS Code — settings, keybindings, tasks, launch config, extensions
Git — .gitconfig, .gitignore_global, user identity
SSH — config, known_hosts, public/private keys
PowerShell — profile scripts, modules, repositories
Node.js / npm, Python, NuGet, .NET SDK, Visual Studio (via vswhere)
Windows Terminal, WSL, environment variables, certificates
Custom file collection — include/exclude rules, size limits, extension filters; skips node_modules, .git, bin/obj, .vs

Pipeline: parallel SHA-256 checksumming → streaming GZip compression → AES-256-GCM chunked encryption (64 KB chunks) → HKDF-SHA256 key derivation → optional PBKDF2-SHA256 password double-wrapping (200,000 iterations) → HMAC-SHA256 container signature.

Features — Restore
Full pipeline: validate → decrypt session key → stream-decrypt payload → decompress → atomic write (.backup_tmp + rename) → SHA-256 verify
Path traversal prevention, archive preview without decryption, "Open Archive" to temp folder, manifest diff comparison between two archives
Scanning & Discovery
Auto-scans Desktop, Downloads, Documents, removable/non-system drives, system drive root
9-step validation: existence → magic bytes → format version → HMAC signature → metadata/manifest parse → session key GCM auth → payload GCM auth → checksum cross-check
System Monitoring & UI
WMI system info, storage/drive monitoring, live scan progress
4-page navigation (Dashboard / Backups / Restore / Settings), custom dark theme, custom title bar
Live EN/RU localization, real-time log (INFO/WARN/ERROR), password protection UI, cancellation support, crash diagnostics
Architecture
Views (XAML) → ViewModels (MVVM, ObservableObject/RelayCommand) → Services (singletons) → Models

Key services: BackupEngine, RestoreEngine, EncryptionService, CompressionService, ChecksumService, BackupContainerService, ArchiveValidationService, ManifestBuilder, MetadataBuilder, SettingsService, LocalizationService, LogService, PasswordProtectionService, SystemInfoService, StorageService, plus 11 snapshot services.

Design decisions: hand-rolled MVVM (no third-party toolkit), all services as singletons, cryptographic key wiping (Array.Clear), thread-safe UI dispatch, no DPAPI/TPM binding (portable across machines), no database/registry/cloud dependency.

.devbackup Container Format
Header (40 bytes: MAGIC "DEVBACKU", VERSION, FLAGS, section lengths)
metadata.json (unencrypted)
manifest.json (unencrypted)
checksums.json (unencrypted)
encrypted.session.key (AES-256-GCM, optionally double-wrapped)
encrypted.payload (GZip → AES-256-GCM, 64 KB chunks)
signature.bin (HMAC-SHA256 over all preceding bytes)
Getting Started

Prerequisites: Windows 10/11 (64-bit), .NET 8 Runtime

Installation: download latest release → extract DevEnvStudio.exe → launch it

First run: app scans for existing .devbackup files → configure backup destination in Settings → click Create Backup

Technology Stack

UI: WPF (.NET 8.0-windows) · Language: C# 12 · Encryption: AES-256-GCM, HKDF-SHA256, PBKDF2-SHA256 · Integrity: HMAC-SHA256, SHA-256 · Compression: GZip · Serialization: System.Text.Json · System Info: WMI · PowerShell SDK v7.4.6 · Localization: live-switchable EN/RU

License

Licensed under the Apache License, Version 2.0 — http://www.apache.org/licenses/LICENSE-2.0. Distributed on an "AS IS" basis, without warranties or conditions of any kind.# DenEnv-Studio
