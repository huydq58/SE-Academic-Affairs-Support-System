/**
 * SE Academic Affairs Support System — Google Apps Script
 * Deploy as Web App: Execute as "Me", Access "Anyone"
 *
 * Sheet "DanhSachDeTai" structure (1-indexed columns):
 *   A: STT  | B: TopicId (DB)  | C: Tên đề tài  | D: Mô tả  | E: Yêu cầu đầu vào
 *   F: Công nghệ  | G: Số SV tối đa  | H: Tên GV  | I: Mã GV
 *   J: MSSV SV  | K: Tên SV  | L: Ghi chú
 *
 * Sheet "BangDiem" structure:
 *   A: MSSV | B: Tên SV | C: Tên đề tài | D: GV hướng dẫn
 *   E: Điểm | F: Người chấm | G: Ngày chấm
 *
 * Supported actions:
 *   GET  ?action=topics&sheetId=...     → danh sách đề tài (DanhSachDeTai)
 *   GET  ?action=grading&sheetId=...    → danh sách chấm điểm (BangDiem)
 *   POST {action:"addTopic", ...}       → thêm đề tài mới, trả rowIndex
 *   POST {action:"register", ...}       → ghi SV đăng ký vào đề tài
 *   POST {action:"grade", ...}          → ghi/cập nhật điểm vào BangDiem
 */

// ─── Entry points ──────────────────────────────────────────────────────────────

function doGet(e) {
  try {
    var action  = e.parameter.action;
    var sheetId = e.parameter.sheetId;
    if (!sheetId) return jsonResponse({ success: false, message: "Thiếu sheetId" });

    if (action === "topics")  return jsonResponse(getTopics(sheetId));
    if (action === "grading") return jsonResponse(getGradingRows(sheetId));

    return jsonResponse({ success: false, message: "Action không hợp lệ: " + action });
  } catch (err) {
    return jsonResponse({ success: false, message: "Lỗi server: " + err.toString() });
  }
}

function doPost(e) {
  try {
    var data   = JSON.parse(e.postData.contents);
    var action = data.action;

    if (action === "addTopic")  return jsonResponse(addTopic(data));
    if (action === "register")  return jsonResponse(registerStudent(data));
    if (action === "grade")     return jsonResponse(gradeStudent(data));

    return jsonResponse({ success: false, message: "Action không hợp lệ: " + action });
  } catch (err) {
    return jsonResponse({ success: false, message: "Lỗi server: " + err.toString() });
  }
}

// ─── addTopic ──────────────────────────────────────────────────────────────────
// Thêm đề tài mới vào sheet DanhSachDeTai và trả về rowIndex của dòng vừa thêm.
// POST body: { action, sheetId, topicId, topicTitle, topicDescription,
//              technologies, requirements, maxStudents, lecturerName, lecturerCode, note }

function addTopic(data) {
  var ss    = SpreadsheetApp.openById(data.sheetId);
  var sheet = ss.getSheetByName("DanhSachDeTai");

  if (!sheet) {
    sheet = ss.insertSheet("DanhSachDeTai");
    // Tạo header
    sheet.appendRow([
      "STT", "TopicId", "Tên đề tài", "Mô tả", "Yêu cầu đầu vào",
      "Công nghệ", "Số SV tối đa", "Tên GV", "Mã GV",
      "MSSV SV", "Tên SV", "Ghi chú"
    ]);
    formatHeader(sheet, 12);
  }

  var lastRow = sheet.getLastRow();            // số dòng hiện tại (kể cả header)
  var stt     = lastRow;                       // STT = số thứ tự (row 1 = header → stt bắt đầu từ 1)
  var newRow  = lastRow + 1;

  sheet.appendRow([
    stt,
    data.topicId       || 0,
    data.topicTitle    || "",
    data.topicDescription || "",
    data.requirements  || "",
    data.technologies  || "",
    data.maxStudents   || 1,
    data.lecturerName  || "",
    data.lecturerCode  || "",
    "",                // MSSV SV (trống khi mới thêm)
    "",                // Tên SV
    data.note          || ""
  ]);

  return { success: true, message: "Đã thêm đề tài: " + data.topicTitle, rowIndex: newRow };
}

// ─── getTopics ─────────────────────────────────────────────────────────────────
// GET ?action=topics&sheetId=...
// Returns array matching TopicSheet model:
//   { rowIndex, stt, topicId, topicName, description, requirements, technologies,
//     maxSlot, lecturer, lecturerInfo, mssv1, student1, note, registered }

function getTopics(sheetId) {
  var ss    = SpreadsheetApp.openById(sheetId);
  var sheet = ss.getSheetByName("DanhSachDeTai");
  if (!sheet) return [];

  var allData = sheet.getDataRange().getValues();
  if (allData.length < 2) return [];

  // Build column map từ header row (chịu được sheet cũ và mới)
  var cm = buildColMap(allData[0]);

  var result = [];
  for (var i = 1; i < allData.length; i++) {
    var row   = allData[i];
    var mssv1 = getCell(row, cm, "mssv sv", "mssv1", "mssv");
    result.push({
      rowIndex     : i + 1,
      stt          : parseInt(getCell(row, cm, "stt")) || i,
      topicId      : parseInt(getCell(row, cm, "topicid")) || 0,
      topicName    : getCell(row, cm, "tên đề tài", "ten de tai"),
      description  : getCell(row, cm, "mô tả", "mo ta"),
      requirements : getCell(row, cm, "yêu cầu đầu vào", "yêu cầu", "yeu cau"),
      technologies : getCell(row, cm, "công nghệ", "cong nghe"),
      maxSlot      : parseInt(getCell(row, cm, "số sv tối đa", "so sv toi da", "số sv")) || 1,
      lecturer     : getCell(row, cm, "tên gv", "ten gv"),
      lecturerInfo : getCell(row, cm, "mã gv", "ma gv"),
      mssv1        : mssv1,
      student1     : getCell(row, cm, "tên sv", "sv1", "sinh vien 1", "tên sinh viên"),
      note         : getCell(row, cm, "ghi chú", "ghi chu"),
      registered   : mssv1 ? 1 : 0
    });
  }

  return result;
}

// ─── registerStudent ───────────────────────────────────────────────────────────
// Ghi thông tin SV vừa đăng ký vào dòng đề tài tương ứng (DanhSachDeTai).
// POST body: { action, sheetId, rowIndex, studentId, studentName }

function registerStudent(data) {
  var ss    = SpreadsheetApp.openById(data.sheetId);
  var sheet = ss.getSheetByName("DanhSachDeTai");
  if (!sheet) return { success: false, message: "Sheet DanhSachDeTai không tồn tại." };

  var rowNum = parseInt(data.rowIndex);
  if (!rowNum || rowNum < 2) return { success: false, message: "rowIndex không hợp lệ." };

  // Xác định cột động từ header — tránh sai khi sheet cũ/mới có cấu trúc khác nhau
  var headers = sheet.getRange(1, 1, 1, sheet.getLastColumn()).getValues()[0];
  var cm      = buildColMap(headers);

  var slotColIdx = findColIdx(cm, "số sv tối đa", "so sv toi da", "số sv");
  var mssvColIdx = findColIdx(cm, "mssv sv", "mssv1", "mssv");
  var nameColIdx = findColIdx(cm, "tên sv", "sv1", "sinh vien 1", "tên sinh viên");

  if (slotColIdx < 0 || mssvColIdx < 0 || nameColIdx < 0) {
    return { success: false, message: "Không tìm thấy cột MSSV SV, Tên SV hoặc Số SV trong header." };
  }

  // 1-indexed column numbers
  var slotCol = slotColIdx + 1;
  var mssvCol = mssvColIdx + 1;
  var nameCol = nameColIdx + 1;

  var maxSlot    = parseInt(sheet.getRange(rowNum, slotCol).getValue()) || 1;
  var mssv1      = (sheet.getRange(rowNum, mssvCol).getValue() || "").toString().trim();
  var registered = mssv1 ? 1 : 0;

  if (registered >= maxSlot) {
    return { success: false, message: "Đề tài đã đủ số lượng sinh viên trên sheet." };
  }

  sheet.getRange(rowNum, mssvCol).setValue(data.studentId   || "");
  sheet.getRange(rowNum, nameCol).setValue(data.studentName || "");

  return { success: true, message: "Đã ghi đăng ký lên sheet." };
}

// ─── writeTopicNote ─────────────────────────────────────────────────────────────
// Ghi ghi chú vào cột L (Ghi chú) của đề tài đã được duyệt (được gọi nội bộ).
function writeTopicNote(sheet, rowIndex, note) {
  if (!sheet || rowIndex < 2) return;
  var existing = (sheet.getRange(rowIndex, 12).getValue() || "").toString().trim();
  var newNote  = existing ? existing + " | " + note : note;
  sheet.getRange(rowIndex, 12).setValue(newNote);
}

// ─── getGradingRows ────────────────────────────────────────────────────────────
// GET ?action=grading&sheetId=...
// Đọc từ sheet BangDiem (lưu điểm)

function getGradingRows(sheetId) {
  var ss    = SpreadsheetApp.openById(sheetId);
  var sheet = ss.getSheetByName("BangDiem");
  if (!sheet) return [];

  var data   = sheet.getDataRange().getValues();
  var result = [];

  for (var i = 1; i < data.length; i++) {
    var row      = data[i];
    var scoreRaw = row[4];
    var score    = (scoreRaw !== "" && scoreRaw !== null) ? parseFloat(scoreRaw) : null;

    result.push({
      rowIndex    : i + 1,
      mssv        : (row[0] || "").toString().trim(),
      studentName : (row[1] || "").toString().trim(),
      topicName   : (row[2] || "").toString().trim(),
      lecturer    : (row[3] || "").toString().trim(),
      score       : score,
      gradedBy    : (row[5] || "").toString().trim(),
      gradedAt    : (row[6] || "").toString().trim()
    });
  }

  return result;
}

// ─── gradeStudent ──────────────────────────────────────────────────────────────
// Ghi hoặc cập nhật điểm vào sheet BangDiem.
// POST body: { action, sheetId, mssv, score, gradedBy, gradedAt,
//              studentName, topicName, lecturer }

function gradeStudent(data) {
  var ss    = SpreadsheetApp.openById(data.sheetId);
  var sheet = ss.getSheetByName("BangDiem");

  if (!sheet) {
    sheet = ss.insertSheet("BangDiem");
    sheet.appendRow([
      "MSSV", "Tên sinh viên", "Tên đề tài", "GV hướng dẫn",
      "Điểm", "Người chấm", "Ngày chấm"
    ]);
    formatHeader(sheet, 7);
  }

  var sheetData = sheet.getDataRange().getValues();
  var mssv      = (data.mssv || "").toString().trim();
  var targetRow = -1;

  for (var i = 1; i < sheetData.length; i++) {
    if ((sheetData[i][0] || "").toString().trim() === mssv) {
      targetRow = i + 1;
      break;
    }
  }

  var gradedAt = data.gradedAt
    || Utilities.formatDate(new Date(), "Asia/Ho_Chi_Minh", "dd/MM/yyyy HH:mm");

  if (targetRow > 0) {
    sheet.getRange(targetRow, 5).setValue(data.score);
    sheet.getRange(targetRow, 6).setValue(data.gradedBy    || "");
    sheet.getRange(targetRow, 7).setValue(gradedAt);
    // Cập nhật tên SV, đề tài, GV nếu có
    if (data.studentName) sheet.getRange(targetRow, 2).setValue(data.studentName);
    if (data.topicName)   sheet.getRange(targetRow, 3).setValue(data.topicName);
    if (data.lecturer)    sheet.getRange(targetRow, 4).setValue(data.lecturer);
  } else {
    sheet.appendRow([
      mssv,
      data.studentName || "",
      data.topicName   || "",
      data.lecturer    || "",
      data.score,
      data.gradedBy    || "",
      gradedAt
    ]);
  }

  return { success: true, message: "Đã lưu điểm cho MSSV: " + mssv };
}

// ─── Helpers ───────────────────────────────────────────────────────────────────

function jsonResponse(data) {
  return ContentService
    .createTextOutput(JSON.stringify(data))
    .setMimeType(ContentService.MimeType.JSON);
}

function formatHeader(sheet, numCols) {
  var range = sheet.getRange(1, 1, 1, numCols || sheet.getLastColumn());
  range.setBackground("#1e3a8a");
  range.setFontColor("#ffffff");
  range.setFontWeight("bold");
  sheet.setFrozenRows(1);
}

// Xây dựng map { headerNameLowercase: columnIndex0Based } từ mảng header
function buildColMap(headerRow) {
  var map = {};
  for (var i = 0; i < headerRow.length; i++) {
    var key = (headerRow[i] || "").toString().trim().toLowerCase();
    if (key) map[key] = i;
  }
  return map;
}

// Tìm index (0-based) của cột theo nhiều tên có thể có
function findColIdx(colMap) {
  var aliases = Array.prototype.slice.call(arguments, 1); // lấy tất cả arg sau colMap
  for (var a = 0; a < aliases.length; a++) {
    var key = (aliases[a] || "").toString().trim().toLowerCase();
    if (colMap[key] !== undefined) return colMap[key];
  }
  return -1;
}

// Đọc giá trị ô từ row theo nhiều tên header có thể có, trả về string
function getCell(row, colMap) {
  var aliases = Array.prototype.slice.call(arguments, 2);
  var idx = findColIdx.apply(null, [colMap].concat(aliases));
  if (idx < 0 || idx >= row.length) return "";
  return (row[idx] || "").toString().trim();
}
