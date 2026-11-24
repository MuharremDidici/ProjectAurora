use aurora_render::RectF;
use taffy::prelude::*;

/// Ergonomic CSS Flexbox & CSS Grid layout wrapper built on `taffy` (used by Bento Deck,
/// 3-Zone Split Fixed Bar, Control Center grids, and Studio Color Customizer).
pub struct FlexGrid {
    tree: TaffyTree<()>,
}

impl Default for FlexGrid {
    fn default() -> Self {
        Self::new()
    }
}

impl FlexGrid {
    pub fn new() -> Self {
        Self { tree: TaffyTree::new() }
    }

    /// Computes exact equal-width or weighted grid cells inside `bounds` using Taffy CSS Grid.
    pub fn compute_grid_cells(
        bounds: RectF,
        cols: usize,
        rows: usize,
        gap_x: f32,
        gap_y: f32,
    ) -> Vec<RectF> {
        let cols = cols.max(1);
        let rows = rows.max(1);
        let mut tree: TaffyTree<()> = TaffyTree::new();
        let mut children = Vec::with_capacity(cols * rows);
        for _ in 0..(cols * rows) {
            if let Ok(n) = tree.new_leaf(Style {
                size: Size { width: auto(), height: auto() },
                ..Default::default()
            }) {
                children.push(n);
            }
        }
        let Ok(root) = tree.new_with_children(
            Style {
                display: Display::Grid,
                align_items: Some(AlignItems::Stretch),
                justify_items: Some(AlignItems::Stretch),
                size: Size { width: length(bounds.w), height: length(bounds.h) },
                grid_template_columns: vec![fr(1.0_f32); cols],
                grid_template_rows: vec![fr(1.0_f32); rows],
                gap: Size { width: length(gap_x), height: length(gap_y) },
                ..Default::default()
            },
            &children,
        ) else {
            return vec![];
        };
        let _ = tree.compute_layout(
            root,
            Size {
                width: AvailableSpace::Definite(bounds.w),
                height: AvailableSpace::Definite(bounds.h),
            },
        );
        children
            .into_iter()
            .filter_map(|n| tree.layout(n).ok())
            .map(|l| RectF::new(bounds.x + l.location.x, bounds.y + l.location.y, l.size.width, l.size.height))
            .collect()
    }

    /// Computes a horizontal or vertical Flexbox row/column with exact Taffy flex-grow / fixed lengths.
    pub fn compute_flex_row(bounds: RectF, item_widths: &[Option<f32>], gap: f32) -> Vec<RectF> {
        let mut tree: TaffyTree<()> = TaffyTree::new();
        let mut children = Vec::with_capacity(item_widths.len());
        for &w in item_widths {
            let style = match w {
                Some(px) => Style {
                    size: Size { width: length(px), height: percent(1.0_f32) },
                    flex_shrink: 0.0,
                    ..Default::default()
                },
                None => Style {
                    size: Size { width: auto(), height: percent(1.0_f32) },
                    flex_grow: 1.0,
                    ..Default::default()
                },
            };
            if let Ok(n) = tree.new_leaf(style) {
                children.push(n);
            }
        }
        let Ok(root) = tree.new_with_children(
            Style {
                display: Display::Flex,
                flex_direction: FlexDirection::Row,
                align_items: Some(AlignItems::Stretch),
                size: Size { width: length(bounds.w), height: length(bounds.h) },
                gap: Size { width: length(gap), height: length(0.0_f32) },
                ..Default::default()
            },
            &children,
        ) else {
            return vec![];
        };
        let _ = tree.compute_layout(
            root,
            Size {
                width: AvailableSpace::Definite(bounds.w),
                height: AvailableSpace::Definite(bounds.h),
            },
        );
        children
            .into_iter()
            .filter_map(|n| tree.layout(n).ok())
            .map(|l| RectF::new(bounds.x + l.location.x, bounds.y + l.location.y, l.size.width, l.size.height))
            .collect()
    }

    pub fn inner(&mut self) -> &mut TaffyTree<()> {
        &mut self.tree
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn taffy_grid_splits_bento_panel_with_exact_gaps() {
        let cells = FlexGrid::compute_grid_cells(RectF::new(10.0, 20.0, 410.0, 210.0), 2, 2, 10.0, 10.0);
        assert_eq!(cells.len(), 4);
        assert!((cells[0].w - 200.0).abs() < 0.5);
        assert!((cells[0].h - 100.0).abs() < 0.5);
        assert!((cells[3].x - 220.0).abs() < 0.5);
        assert!((cells[3].y - 130.0).abs() < 0.5);
    }
}
