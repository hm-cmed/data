/**
 * Codemed-x 学習履歴レシーバ（Google Apps Script / スプレッドシート版）
 *
 * 本格的な LMS を用意する前に、Google スプレッドシートを受け皿にして
 * 5 シナリオ共通の TrainingEvent を蓄積するための最小実装。
 *
 * セットアップ:
 *   1. 受け皿にするスプレッドシートを作成し、拡張機能 > Apps Script でこのファイルを貼る
 *   2. プロジェクトの設定 > スクリプト プロパティ に以下を登録
 *        SHARED_TOKEN : Unity 側 EventLogger.SetAuthToken() に渡すのと同じ文字列
 *        SHEET_NAME   : 書き込み先シート名（省略時 "events"）
 *   3. デプロイ > 新しいデプロイ > 種類「ウェブアプリ」
 *        実行するユーザー : 自分
 *        アクセスできるユーザー : 全員
 *   4. 発行された /exec の URL を EventLoggerSettings.endpointUrl に設定
 *
 * 注意:
 *   - Apps Script のウェブアプリは Authorization ヘッダを受け取れないため、トークンは
 *     クエリパラメータ ?token=... で受ける。Unity 側は EventLoggerSettings の
 *     認証方式を「クエリパラメータ」にしておくこと（URL には焼き込まず実行時に付与される）。
 *   - クエリ文字列はアクセスログに残りうる。トークンは学習履歴の投稿専用とし、
 *     定期的にローテーションする前提で運用する。
 *   - スプレッドシートは 1 シート 1,000 万セルの上限がある。恒常運用では
 *     BigQuery / Cloud SQL への移行を前提に、本スクリプトは実証段階向けとする。
 */

var COLUMNS = [
  'received_at_iso',
  'schema_version',
  'user_id',
  'session_id',
  'scenario_id',
  'event_type',
  'objective_id',
  'event_timestamp_unix_ms',
  'event_timestamp_iso',
  'sequence',
  'score',
  'error_type',
  'device_model',
  'build_version',
  'locomotion_mode',
  'payload_json',
  'batch_id'
];

function doPost(e) {
  var lock = LockService.getScriptLock();

  try {
    // 同時書き込みで行が壊れるのを防ぐ。演習では 30 台が同時に送ってくる。
    lock.waitLock(20000);
  } catch (lockError) {
    return jsonResponse({ error: 'busy' });
  }

  try {
    if (!e || !e.postData || !e.postData.contents) {
      return jsonResponse({ error: 'empty_body' });
    }

    var batch;
    try {
      batch = JSON.parse(e.postData.contents);
    } catch (parseError) {
      return jsonResponse({ error: 'invalid_json' });
    }

    var expectedToken = PropertiesService.getScriptProperties().getProperty('SHARED_TOKEN');
    var presentedToken = e.parameter ? e.parameter.token : null;
    if (expectedToken && presentedToken !== expectedToken) {
      return jsonResponse({ error: 'unauthorized' });
    }

    var violations = validateBatch(batch);
    if (violations.length > 0) {
      return jsonResponse({ error: 'schema_violation', violations: violations.slice(0, 10) });
    }

    if (isDuplicateBatch(batch.batch_id)) {
      return jsonResponse({ status: 'duplicate_ignored', batch_id: batch.batch_id });
    }

    var sheet = getEventSheet();
    var receivedAt = new Date().toISOString();
    var rows = batch.events.map(function (event) {
      return toRow(event, batch.batch_id, receivedAt);
    });

    sheet.getRange(sheet.getLastRow() + 1, 1, rows.length, COLUMNS.length).setValues(rows);
    rememberBatch(batch.batch_id);

    return jsonResponse({ status: 'accepted', batch_id: batch.batch_id, accepted: rows.length });
  } finally {
    lock.releaseLock();
  }
}

function doGet() {
  return jsonResponse({ status: 'ok', schema_version: '1.0' });
}

function validateBatch(batch) {
  var violations = [];

  if (batch.schema_version !== '1.0') {
    violations.push('schema_version が 1.0 ではありません');
  }

  if (!batch.batch_id) {
    violations.push('batch_id がありません');
  }

  if (!batch.events || !batch.events.length) {
    violations.push('events が空です');
  } else {
    for (var i = 0; i < batch.events.length; i++) {
      var event = batch.events[i];
      if (!event.user_id || !event.session_id || !event.scenario_id || !event.event_type) {
        violations.push('events[' + i + ']: 必須フィールドが欠けています');
      }
      if (typeof event.event_timestamp_unix_ms !== 'number') {
        violations.push('events[' + i + ']: event_timestamp_unix_ms が数値ではありません');
      }
    }
  }

  return violations;
}

function toRow(event, batchId, receivedAt) {
  var eventIso = new Date(event.event_timestamp_unix_ms).toISOString();

  return [
    receivedAt,
    event.schema_version || '',
    event.user_id || '',
    event.session_id || '',
    event.scenario_id || '',
    event.event_type || '',
    event.objective_id || '',
    event.event_timestamp_unix_ms || 0,
    eventIso,
    typeof event.sequence === 'number' ? event.sequence : -1,
    typeof event.score === 'number' ? event.score : 0,
    event.error_type || 'none',
    event.device_model || '',
    event.build_version || '',
    event.locomotion_mode || '',
    event.payload_json || '',
    batchId
  ];
}

function getEventSheet() {
  var spreadsheet = SpreadsheetApp.getActiveSpreadsheet();
  var sheetName = PropertiesService.getScriptProperties().getProperty('SHEET_NAME') || 'events';
  var sheet = spreadsheet.getSheetByName(sheetName);

  if (!sheet) {
    sheet = spreadsheet.insertSheet(sheetName);
  }

  if (sheet.getLastRow() === 0) {
    sheet.getRange(1, 1, 1, COLUMNS.length).setValues([COLUMNS]);
    sheet.setFrozenRows(1);
  }

  return sheet;
}

/**
 * batch_id をキャッシュに 6 時間保持して再送を冪等に扱う。
 * 端末は通信断のたびに同じ batch_id で送り直してくるため、これが無いと行が重複する。
 */
function isDuplicateBatch(batchId) {
  return CacheService.getScriptCache().get('batch:' + batchId) !== null;
}

function rememberBatch(batchId) {
  CacheService.getScriptCache().put('batch:' + batchId, '1', 21600);
}

function jsonResponse(body) {
  return ContentService
    .createTextOutput(JSON.stringify(body))
    .setMimeType(ContentService.MimeType.JSON);
}
