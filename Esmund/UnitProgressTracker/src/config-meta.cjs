'use strict';

function coerceSkidNumberValue(v) {
  if (typeof v === 'number' && Number.isFinite(v)) return v;
  if (typeof v === 'string') {
    const n = parseFloat(String(v).trim());
    return Number.isFinite(n) ? n : null;
  }
  return null;
}

function extractSkidNumberFromConfig(configJson, depth = 0) {
  if (depth > 8 || !configJson || typeof configJson !== 'object') return null;

  const conf = configJson.configuration;
  if (conf && typeof conf === 'object') {
    if (conf.wall && typeof conf.wall === 'object' && Object.prototype.hasOwnProperty.call(conf.wall, 'skidNumber')) {
      const fromWall = coerceSkidNumberValue(conf.wall.skidNumber);
      if (fromWall != null) return fromWall;
    }
  }

  if (Object.prototype.hasOwnProperty.call(configJson, 'skidNumber')) {
    const v = coerceSkidNumberValue(configJson.skidNumber);
    if (v != null) return v;
  }

  if (conf && typeof conf === 'object' && Object.prototype.hasOwnProperty.call(conf, 'skidNumber')) {
    const v = coerceSkidNumberValue(conf.skidNumber);
    if (v != null) return v;
  }

  for (const key of ['configuration', 'config', 'data', 'surface', 'metadata', 'properties', 'wall']) {
    const inner = configJson[key];
    if (inner && typeof inner === 'object') {
      const found = extractSkidNumberFromConfig(inner, depth + 1);
      if (found != null) return found;
    }
  }

  return null;
}

function mapSkidNumberToSkidId(rawSkidNumber) {
  const n = Number(rawSkidNumber);
  if (!Number.isFinite(n)) return 1;
  return n + 1;
}

function extractConfigurationKind(configJson) {
  const conf = configJson?.configuration;
  if (!conf || typeof conf !== 'object') return '';

  const typeStr = typeof conf.$type === 'string' ? conf.$type : '';
  const typeMatch = /CConfiguration_(\w+)/i.exec(typeStr);
  if (typeMatch) {
    const raw = typeMatch[1];
    if (raw.toLowerCase() === 'unitbase') return 'UnitBase';
    return raw.charAt(0).toUpperCase() + raw.slice(1).toLowerCase();
  }

  for (const key of ['roof', 'wall', 'unitBase']) {
    if (!conf[key] || typeof conf[key] !== 'object') continue;
    if (key === 'unitBase') {
      if (Array.isArray(conf.unitBase.unitBaseGeometryList) && conf.unitBase.unitBaseGeometryList.length) {
        return 'UnitBase';
      }
    } else if (Array.isArray(conf[key].geometryList) && conf[key].geometryList.length) {
      return key.charAt(0).toUpperCase() + key.slice(1);
    }
  }

  return '';
}

function extractSurfaceScanMeta(configJson) {
  const skidNumber = extractSkidNumberFromConfig(configJson, 0);
  const skidId = skidNumber != null ? mapSkidNumberToSkidId(skidNumber) : null;
  const configurationKind = extractConfigurationKind(configJson);
  return { skidNumber, skidId, configurationKind };
}

module.exports = {
  extractSurfaceScanMeta,
  extractSkidNumberFromConfig,
  mapSkidNumberToSkidId,
  extractConfigurationKind,
};
