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

function resolvePythonCommand() {
  if (process.env.USV_PYTHON && process.env.USV_PYTHON.trim()) {
    return process.env.USV_PYTHON.trim();
  }
  return process.platform === 'win32' ? 'python' : 'python3';
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

  try {
    const python = resolvePythonCommand();
    emitScanProgress(onProgress, {
      phase: 'reading',
      current: 0,
      total: iamPaths.length,
      message: `Starting Inventor read (0/${iamPaths.length})…`,
    });

    const timeoutMs =
      typeof options.timeoutMs === 'number' && options.timeoutMs > 0
        ? options.timeoutMs
        : DEFAULT_SCAN_TIMEOUT_MS;

    const parsed = await new Promise((resolve, reject) => {
      const proc = spawn(python, [scriptPath, '--paths-file', tempFile], { windowsHide: true });
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
            const index = iamPaths.findIndex((p) => path.normalize(p) === path.normalize(iamPath));
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
    });

    if (!parsed || typeof parsed !== 'object') {
      throw new Error('Inventor reader returned empty result');
    }
    if (parsed.error && !parsed.surfaces?.length) {
      throw new Error(parsed.error);
    }
    parsed._stderr = parsed._stderr || '';
    return parsed;
  } finally {
    try {
      fs.unlinkSync(tempFile);
    } catch {
      /* ignore */
    }
  }
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
  resolvePythonScript,
  is391ZSurfaceIam,
  extractSkidHintFromPath,
  cancelActiveScan,
  beginScanSession,
  isScanCancelled,
};
