"""
Table structure detection from OCR text blocks.

Groups blocks into rows by Y-coordinate proximity, identifies rows with 2+
distinct X-position clusters as tabular, then groups consecutive tabular rows
into TableBlock objects.  No image processing required.
"""

import logging

from models import BoundingBox, TableBlock, TableCell, TextBlock

logger = logging.getLogger(__name__)

_ROW_Y_TOLERANCE = 12   # pixels — blocks within this vertical distance share a row
_COL_X_GAP       = 30   # pixels — X gap larger than this separates column clusters
_MIN_COLS        = 2    # minimum column clusters to treat a row as tabular
_MIN_ROWS        = 2    # minimum consecutive tabular rows to form a table


def detect_tables(text_blocks: list[TextBlock], page: int) -> list[TableBlock]:
    """Return table blocks detected on the given page.

    Uses only the positions of existing text blocks — no image processing.
    """
    page_blocks = [b for b in text_blocks if b.page == page]
    if not page_blocks:
        return []

    rows             = _cluster_into_rows(page_blocks)
    table_row_groups = _find_table_row_groups(rows)

    tables: list[TableBlock] = []
    for group in table_row_groups:
        tbl = _build_table_block(group, page)
        if tbl is not None:
            tables.append(tbl)
            logger.info(
                "Table detected on page %d — %d rows × %d cols",
                page, tbl.rows, tbl.cols,
            )

    return tables


# ── Internal helpers ───────────────────────────────────────────────────────────

def _cluster_into_rows(blocks: list[TextBlock]) -> list[list[TextBlock]]:
    """Group blocks into horizontal rows by Y-proximity."""
    sorted_blocks = sorted(blocks, key=lambda b: b.bounding_box.y)
    rows: list[list[TextBlock]] = []

    for block in sorted_blocks:
        placed = False
        for row in rows:
            row_y = sum(b.bounding_box.y for b in row) / len(row)
            if abs(block.bounding_box.y - row_y) <= _ROW_Y_TOLERANCE:
                row.append(block)
                placed = True
                break
        if not placed:
            rows.append([block])

    for row in rows:
        row.sort(key=lambda b: b.bounding_box.x)

    return sorted(rows, key=lambda r: min(b.bounding_box.y for b in r))


def _count_col_clusters(row: list[TextBlock]) -> int:
    """Count distinct X-position clusters (columns) in a row."""
    if not row:
        return 0
    xs       = sorted(b.bounding_box.x for b in row)
    clusters = 1
    for i in range(1, len(xs)):
        if xs[i] - xs[i - 1] > _COL_X_GAP:
            clusters += 1
    return clusters


def _find_table_row_groups(rows: list[list[TextBlock]]) -> list[list[list[TextBlock]]]:
    """Find groups of consecutive multi-column rows."""
    groups:  list[list[list[TextBlock]]] = []
    current: list[list[TextBlock]]       = []

    for row in rows:
        if _count_col_clusters(row) >= _MIN_COLS:
            current.append(row)
        else:
            if len(current) >= _MIN_ROWS:
                groups.append(current)
            current = []

    if len(current) >= _MIN_ROWS:
        groups.append(current)

    return groups


def _cluster_xs(xs: list[int]) -> list[int]:
    """Collapse sorted X values into representative column positions."""
    if not xs:
        return []
    clusters = [xs[0]]
    for x in xs[1:]:
        if x - clusters[-1] > _COL_X_GAP:
            clusters.append(x)
    return clusters


def _nearest_col(x: int, col_xs: list[int]) -> int:
    """Return the index of the column position nearest to x."""
    best, best_dist = 0, abs(x - col_xs[0])
    for i, cx in enumerate(col_xs[1:], 1):
        d = abs(x - cx)
        if d < best_dist:
            best, best_dist = i, d
    return best


def _build_table_block(
    row_group: list[list[TextBlock]], page: int
) -> TableBlock | None:
    """Assemble a TableBlock from a confirmed group of tabular rows."""
    if not row_group:
        return None

    all_xs  = sorted({b.bounding_box.x for row in row_group for b in row})
    col_xs  = _cluster_xs(all_xs)

    cells: list[TableCell] = []
    for row_idx, row in enumerate(row_group):
        for block in row:
            col_idx = _nearest_col(block.bounding_box.x, col_xs)
            cells.append(
                TableCell(
                    row=row_idx,
                    col=col_idx,
                    text=block.text,
                    bounding_box=block.bounding_box,
                )
            )

    all_flat = [b for row in row_group for b in row]
    min_x    = min(b.bounding_box.x for b in all_flat)
    min_y    = min(b.bounding_box.y for b in all_flat)
    max_x    = max(b.bounding_box.x + b.bounding_box.width  for b in all_flat)
    max_y    = max(b.bounding_box.y + b.bounding_box.height for b in all_flat)

    return TableBlock(
        page=page,
        rows=len(row_group),
        cols=len(col_xs),
        cells=cells,
        bounding_box=BoundingBox(
            x=min_x, y=min_y, width=max_x - min_x, height=max_y - min_y
        ),
    )
