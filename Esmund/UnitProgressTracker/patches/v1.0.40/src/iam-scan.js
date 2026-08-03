'use strict';

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const DEFAULT_SCAN_TIMEOUT_MS = 15 * 60 * 1000;

let activeScanProcess = null;
let activeScanCancelled = false;

const SKIP_FOLDERS = new Set([
  'oldversions',
  'archive',
  'archived',
  'backup',
  'backups',
  'temp',
  'tmp',
  '_restore',
  '.unit-surface-viewer',
]);

function shouldSkipFolder(name) {
  if (!name) return true;
  if (name.startsWith('.')) return true;
  return SKIP_FOLDERS.has(name.toLowerCase());
}

function stripExtendedPathPrefix(filePath) {
  if (process.platform !== 'win32' || !filePath) return filePath;
  if (filePath.startsWith('\\\\?\\')) return filePath.slice(4);
  return filePath;
}

function resolveIamPath(filePath) {
  if (!filePath) return '';
  try {
    if (typeof fs.realpathSync.native === 'function') {
      return stripExtendedPathPrefix(fs.realpathSync.native(filePath));
    }
    return stripExtendedPathPrefix(fs.realpathSync(filePath));
  } catch {
    return stripExtendedPathPrefix(path.resolve(filePath));
  }
}

/** Stable map key for IAM paths (Python resolves paths; picker/walk may differ). */
function iamPathKey(filePath) {
  const resolved = resolveIamPath(filePath);
  return process.platform === 'win32' ? resolved.toLowerCase() : resolved;
}

function iamBasenameKey(filePath) {
  return path.basename(String(filePath || '')).toLowerCase();
}

function is391ZSurfaceIam(filePath) {
  const base = path.basename(filePath);
  return /^391Z.+\.iam$/i.test(base) && !/^391-.+\.iam$/i.test(base);
}

function walk391ZIamFiles(rootDir, maxDepth = 12) {
  const results = [];

  function scanFolder(folder, depth) {
    if (depth > maxDepth) return;
    let entries;
    try {
      entries = fs.readdirSync(folder, { withFileTypes: true });
    } catch {
      return;
    }

    for (const entry of entries) {
      const full = path.join(folder, entry.name);
      if (entry.isFile() && is391ZSurfaceIam(full)) {
        if (!full.includes(`${path.sep}OldVersions${path.sep}`) && !full.includes(`${path.sep}oldversions${path.sep}`)) {
          results.push(full);
        }
      }
    }

    if (depth >= maxDepth) return;

    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      if (shouldSkipFolder(entry.name)) continue;
      scanFolder(path.join(folder, entry.name), depth + 1);
    }
  }

  scanFolder(rootDir, 0);
  results.sort((a, b) => a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' }));
  return results;
}

function resolveSidecarExe(appRoot) {
  const candidates = [
    path.join(appRoot, 'sidecar', 'SurfaceMomSidecar.exe'),
    path.join(appRoot, 'sidecar', 'bin', 'Release', 'net48', 'SurfaceMomSidecar.exe'),
    path.join(appRoot, '..', 'Ce3', 'tools', 'Surface_Config_Editor', 'sidecar', 'bin', 'Release', 'net48', 'SurfaceMomSidecar.exe'),
  ];
  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) return candidate;
  }
  return null;
}

function resolvePythonScript(appRoot) {
  const candidate = path.join(appRoot, 'sidecar', 'inventor-config-read.py');
  return fs.existsSync(candidate) ? candidate : null;
}

function resolvePythonCommands() {
  /** @type {string[][]} */
  const commands = [];

  const envPython = process.env.USV_PYTHON && process.env.USV_PYTHON.trim();
  if (envPython) {
    if (/\.exe$/i.test(envPython) || envPython.includes('\\') || envPython.includes('/')) {
      commands.push([envPython]);
    } else {
      commands.push(envPython.split(/\s+/).filter(Boolean));
    }
  }

  if (process.platform === 'win32') {
    commands.push(['py', '-3']);
    commands.push(['python']);
    const localAppData = process.env.LOCALAPPDATA;
    if (localAppData) {
      const pyRoot = path.join(localAppData, 'Programs', 'Python');
      try {
        if (fs.existsSync(pyRoot)) {
          for (const dir of fs.readdirSync(pyRoot)) {
            const exe = path.join(pyRoot, dir, 'python.exe');
            if (fs.existsSync(exe)) commands.push([exe]);
          }
        }
      } catch {
        /* ignore */
      }
    }
  } else {
    commands.push(['python3']);
    commands.push(['python']);
  }

  const seen = new Set();
  return commands.filter((parts) => {
    const key = parts.join('\0');
    if (seen.has(key)) return false;
    seen.add(key);
    return parts.length > 0 && Boolean(parts[0]);
  });
}

function resolvePythonCommand() {
  const commands = resolvePythonCommands();
  return commands.length ? commands[0] : process.platform === 'win32' ? ['python'] : ['python3'];
}

function formatPythonCommand(parts) {
  return Array.isArray(parts) ? parts.join(' ') : String(parts);
}

function runProcess(command, args, options = {}) {
  return new Promise((resolve, reject) => {
    const proc = spawn(command, args, { windowsHide: true, ...options });
    let stdout = '';
    let stderr = '';
    proc.stdout.on('data', (chunk) => {
      stdout += chunk.toString();
    });
    proc.stderr.on('data', (chunk) => {
      stderr += chunk.toString();
    });
    proc.on('error', reject);
    proc.on('close', (code) => {
      resolve({ code, stdout, stderr });
    });
  });
}

function extractSkidHintFromPath(filePath) {
  const parts = String(filePath || '').split(/[/\\]/);
  for (let i = parts.length - 1; i >= 0; i--) {
    if (/^skid\s+\d+$/i.test(parts[i])) return parts[i];
  }
  return '';
}

function emitScanProgress(onProgress, payload) {
  if (typeof onProgress === 'function') onProgress(payload);
}

function cancelActiveScan() {
  activeScanCancelled = true;
  if (!activeScanProcess) return;
  try {
    activeScanProcess.kill();
  } catch {
    /* ignore */
  }
  activeScanProcess = null;
}

function beginScanSession() {
  activeScanCancelled = false;
  activeScanProcess = null;
}

function isScanCancelled() {
  return activeScanCancelled;
}

function parseProgressLine(line) {
  const trimmed = String(line || '').trim();
  if (!trimmed.startsWith('{')) return null;
  try {
    const data = JSON.parse(trimmed);
    return data && data.type === 'progress' ? data : null;
  } catch {
    return null;
  }
}

async function readConfigsViaPython(appRoot, iamPaths, onProgress, options = {}) {
  const scriptPath = resolvePythonScript(appRoot);
  if (!scriptPath || iamPaths.length === 0) return null;

  const tempFile = path.join(os.tmpdir(), `usv-iam-paths-${Date.now()}.txt`);
  fs.writeFileSync(tempFile, iamPaths.join('\n'), 'utf8');

  const pythonCommands = resolvePythonCommands();
  if (!pythonCommands.length) return null;

  const timeoutMs =
    typeof options.timeoutMs === 'number' && options.timeoutMs > 0
      ? options.timeoutMs
      : DEFAULT_SCAN_TIMEOUT_MS;

  const spawnErrors = [];

  try {
    for (let ci = 0; ci < pythonCommands.length; ci++) {
      const commandParts = pythonCommands[ci];
      const [command, ...commandPrefixArgs] = commandParts;
      const args = [...commandPrefixArgs, scriptPath, '--paths-file', tempFile];
      const label = formatPythonCommand(commandParts);

      emitScanProgress(onProgress, {
        phase: 'reading',
        current: 0,
        total: iamPaths.length,
        message: `Starting Inventor read via ${label} (0/${iamPaths.length})…`,
      });

      let parsed;
      try {
        parsed = await runPythonInventorRead(command, args, iamPaths, onProgress, timeoutMs);
      } catch (err) {
        const msg = err.message || String(err);
        spawnErrors.push(`${label}: ${msg}`);
        const retryable =
          /ENOENT|not found|spawn/i.test(msg) ||
          /Microsoft Store|run without arguments to install/i.test(msg);
        if (retryable && ci < pythonCommands.length - 1) continue;
        if (ci < pythonCommands.length - 1) continue;
        throw new Error(
          `Could not run Inventor Python reader. Tried: ${spawnErrors.join('; ')}. ` +
            'Install Python 3 with pywin32, use "py -3", or set USV_PYTHON to your python.exe path.'
        );
      }

      parsed._pythonCommand = label;
      return parsed;
    }

    throw new Error(
      `Could not run Inventor Python reader. Tried: ${spawnErrors.join('; ') || pythonCommands.map(formatPythonCommand).join(', ')}`
    );
  } finally {
    try {
      fs.unlinkSync(tempFile);
    } catch {
      /* ignore */
    }
  }
}

function runPythonInventorRead(command, args, iamPaths, onProgress, timeoutMs) {
  return new Promise((resolve, reject) => {
    const proc = spawn(command, args, { windowsHide: true });
    activeScanProcess = proc;
    let stdout = '';
    let stderr = '';
    let stderrBuffer = '';
    let settled = false;

    const finish = (handler) => {
      if (settled) return;
      settled = true;
      clearTimeout(timeoutId);
      if (activeScanProcess === proc) activeScanProcess = null;
      handler();
    };

    const timeoutId = setTimeout(() => {
      cancelActiveScan();
      finish(() => reject(new Error(`Inventor scan timed out after ${Math.round(timeoutMs / 60000)} minutes.`)));
    }, timeoutMs);

    proc.stdout.on('data', (chunk) => {
      stdout += chunk.toString();
    });

    proc.stderr.on('data', (chunk) => {
      stderr += chunk.toString();
      stderrBuffer += chunk.toString();
      const lines = stderrBuffer.split(/\r?\n/);
      stderrBuffer = lines.pop() || '';
      for (const line of lines) {
        const progress = parseProgressLine(line);
        if (progress) {
          emitScanProgress(onProgress, {
            phase: progress.phase || 'reading',
            current: progress.current,
            total: progress.total || iamPaths.length,
            filePath: progress.iamPath,
            skid: progress.skid || extractSkidHintFromPath(progress.iamPath),
            surface: progress.surface,
            message: progress.message,
          });
          continue;
        }
        const legacy = line.match(/^Reading (.+)$/);
        if (legacy) {
          const iamPath = legacy[1].trim();
          const index = iamPaths.findIndex((p) => iamPathKey(p) === iamPathKey(iamPath));
          emitScanProgress(onProgress, {
            phase: 'reading',
            current: index >= 0 ? index + 1 : undefined,
            total: iamPaths.length,
            filePath: iamPath,
            skid: extractSkidHintFromPath(iamPath),
            surface: path.basename(iamPath, path.extname(iamPath)),
          });
        }
      }
    });

    proc.on('error', (err) => finish(() => reject(err)));
    proc.on('close', (code) => {
      if (isScanCancelled()) {
        finish(() => reject(new Error('Scan cancelled')));
        return;
      }
      finish(() => {
        if (/Microsoft Store|run without arguments to install/i.test(stderr)) {
          reject(new Error(`Python command failed (${command}): ${stderr.trim().split(/\r?\n/)[0]}`));
          return;
        }
        try {
          const result = JSON.parse(stdout.trim() || '{}');
          result._stderr = stderr;
          result._exitCode = code;
          resolve(result);
        } catch (err) {
          reject(
            new Error(
              `Inventor reader returned invalid JSON (${err.message}). ${stderr.trim() || stdout.trim()}`.slice(
                0,
                500
              )
            )
          );
        }
      });
    });
  }).then((parsed) => {
    if (!parsed || typeof parsed !== 'object') {
      throw new Error('Inventor reader returned empty result');
    }
    if (parsed.error && !parsed.surfaces?.length) {
      throw new Error(parsed.error);
    }
    if (!parsed.surfaces?.length && !parsed.errors?.length && iamPaths.length) {
      const detail = String(parsed._stderr || '').trim();
      throw new Error(
        `Inventor reader returned no surfaces or errors (exit ${parsed._exitCode ?? '?'}).${
          detail ? ` ${detail.slice(-400)}` : ''
        }`
      );
    }
    parsed._stderr = parsed._stderr || '';
    return parsed;
  });
}

function runSidecar(exePath, args) {
  return runProcess(exePath, args).then(({ code, stdout, stderr }) => {
    if (code !== 0 && code !== 2) {
      throw new Error(stderr.trim() || stdout.trim() || `Sidecar exited ${code}`);
    }
    try {
      return JSON.parse(stdout.trim() || '{}');
    } catch (err) {
      throw new Error(`Sidecar JSON parse failed: ${err.message}`);
    }
  });
}

async function readConfigViaSidecar(exePath, iamPath) {
  let parsed;
  try {
    parsed = await runSidecar(exePath, ['--mom-read', '--file', iamPath]);
  } catch (err) {
    if (/No running Inventor instance/i.test(String(err.message))) {
      parsed = await runSidecar(exePath, ['--launch', '--mom-read', '--file', iamPath]);
    } else {
      throw err;
    }
  }
  if (!parsed?.Ok) {
    throw new Error(parsed?.Error || 'Sidecar could not read IAM');
  }
  const raw = parsed?.Attributes?.DOCUMENT_CONFIG_JSON;
  if (!raw || !String(raw).trim()) {
    throw new Error('IAM has no DOCUMENT_CONFIG_JSON attribute');
  }
  const config = JSON.parse(String(raw));
  if (!config?.configuration) {
    throw new Error('DOCUMENT_CONFIG_JSON missing configuration block');
  }
  return config;
}

async function readConfigsViaSidecar(appRoot, iamPaths, onProgress) {
  const exePath = resolveSidecarExe(appRoot);
  if (!exePath) return null;

  const surfaces = [];
  const errors = [];
  for (let i = 0; i < iamPaths.length; i++) {
    if (isScanCancelled()) {
      throw new Error('Scan cancelled');
    }
    const iamPath = iamPaths[i];
    emitScanProgress(onProgress, {
      phase: 'reading',
      current: i + 1,
      total: iamPaths.length,
      filePath: iamPath,
      skid: extractSkidHintFromPath(iamPath),
      surface: path.basename(iamPath, path.extname(iamPath)),
    });
    try {
      const config = await readConfigViaSidecar(exePath, iamPath);
      surfaces.push({ iamPath, config });
    } catch (err) {
      errors.push({ iamPath, error: err.message || String(err) });
    }
  }
  return { ok: surfaces.length > 0, surfaces, errors, source: 'sidecar' };
}

async function readConfigsFromIams(appRoot, iamPaths, onProgress, options = {}) {
  if (!iamPaths.length) {
    return { ok: true, surfaces: [], errors: [], source: 'none' };
  }

  beginScanSession();
  let result = await readConfigsViaPython(appRoot, iamPaths, onProgress, options);
  if (!result) {
    result = await readConfigsViaSidecar(appRoot, iamPaths, onProgress);
  }
  if (!result) {
    throw new Error(
      'Could not read IAM config. Requires Autodesk Inventor and Python with pywin32 (same as Pigeon), ' +
        'or SurfaceMomSidecar.exe in sidecar/.'
    );
  }
  result.source = result.source || 'inventor-python';
  return result;
}

module.exports = {
  walk391ZIamFiles,
  readConfigsFromIams,
  resolveSidecarExe,
  resolvePythonCommand,
  resolvePythonCommands,
  resolveIamPath,
  iamPathKey,
  iamBasenameKey,
  is391ZSurfaceIam,
  extractSkidHintFromPath,
  cancelActiveScan,
  beginScanSession,
  isScanCancelled,
};
