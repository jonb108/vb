use warnings;
use strict;
package VB_SVG;
use base 'Exporter';
our @EXPORT_OK = qw/
    msg2svg
/;

# the string to accumulate all svg elements
my $SVG;

# SVG margins
my $top_margin = 2;
my $bottom_margin = 2;
my $left_margin = 2;
my $right_margin = 2;

# Blackboard dimensions
my $blackboard_width = 50;
my $blackboard_height = 60;
my $blackboard_font_size = "50px";

# Whiteboard dimensions (smaller boards, smaller letters)
my $whiteboard_width = 40;
my $whiteboard_height = 50;
my $whiteboard_font_size = "36px";

# PVC pipe and joiner dimensions
my $pvc_pipe_width = 2;
my $pvc_joiner_width = $pvc_pipe_width + 2;
my $coupler_width = $pvc_joiner_width * 2;
my $coupler_height = $pvc_joiner_width;
my $tee_width = $pvc_joiner_width * 3;
my $tee_height = $pvc_joiner_width * 2;
my $v_pole_length = $blackboard_height + 10;
my $tee_pole_height = $tee_height + $v_pole_length;
my $h_pole_length = 2*$blackboard_width;
my $h_pole_short_length = $h_pole_length / 2;

# Board vertical offset from top of PVC frame
my $board_v_offset = 9;

# Shared text styles
my $text_font_family = "monospace";
my $text_weight = "bold";

sub svg_styles {
    $SVG .= <<EOF;
<style>
text {
    font-family: $text_font_family;
    font-weight: $text_weight;
}
text.blackboard {
    font-size: $blackboard_font_size;
    fill: white;
}
text.whiteboard {
    font-size: $whiteboard_font_size;
}

rect.blackboard {
    width: ${blackboard_width}px;
    height: ${blackboard_height}px;
    fill: black;
    stroke: black;
}
rect.whiteboard {
    width: ${whiteboard_width}px;
    height: ${whiteboard_height}px;
    fill: white;
    stroke: black;
}
.pvc {
    stroke: black;
    fill: white;
}
</style>
EOF
}

sub margin_wrapper_start {
    my ($left_margin, $top_margin) = @_;
    $SVG .= "<g transform='translate($left_margin,$top_margin)'>\n";
}

sub margin_wrapper_end {
    $SVG .= "</g>\n";
}

# Tee and vertical pole - return width for tracking position
#
sub tee_pole {
    my ($x, $y) = @_;
    $SVG .= "<g transform='translate($x,$y)'>\n";
    my $w1 = $pvc_joiner_width;
    my $w2 = $pvc_joiner_width * 2;
    my $w3 = $pvc_joiner_width * 3;
    # Tee
    $SVG .= "<polyline class='pvc' points='0,0 $w3,0 $w3,$w1 $w2,$w1 $w2,$w2 $w1,$w2 $w1,$w1 0,$w1 0,0' />\n";
    # Vertical pole
    $SVG .= "<rect class='pvc' x='" . ($w1+1) . "' y='$w2' width='$pvc_pipe_width' height='$v_pole_length' />\n";
    $SVG .= "</g>\n";
    return $tee_width;
}

# Horizontal pole - return width for tracking position
#
sub h_pole {
    my ($x, $y, $length) = @_;
    $SVG .= "<rect class='pvc' x='$x' y='$y' width='$length' height='$pvc_pipe_width' />\n";
    return $length;
}

# Coupler - return width for tracking position
#
sub coupler {
    my ($x, $y) = @_;
    $SVG .= "<rect class='pvc' x='$x' y='$y' width='$coupler_width' height='$coupler_height' />\n";
    return $coupler_width;
}

# Tee poles on the ends and every 4 letters at most.
# Two horizontal poles with a coupler
# between for every 4 letters, one horizontal pole
# for every 2 letters, and a short horizontal
# pole for a leftover single letter.
# Left tee and pole
#
sub pvc_frame {
    my ($message) = @_;
    my $x = tee_pole(0, 0);
    # Horizontal pipes and couplers
    my $length = length($message);
    for (my $i = 0; $i < $length; $i += 4) {
        my $remaining = $length - $i;
        if ($remaining >= 4) {
            $x += h_pole($x, 1, $h_pole_length);
            $x += coupler($x, 0);
            $x += h_pole($x, 1, $h_pole_length);
        } elsif ($remaining == 3) {
            $x += h_pole($x, 1, $h_pole_length);
            $x += coupler($x, 0);
            $x += h_pole($x, 1, $h_pole_short_length);
        } elsif ($remaining == 2) {
            $x += h_pole($x, 1, $h_pole_length);
        } elsif ($remaining == 1) {
            $x += h_pole($x, 1, $h_pole_short_length);
        }
        $x += tee_pole($x, 0);
    }
    return $x;
}

# Return the width of the PVC horizontal
# pipe(s and coupler if needed) for $remaining letters
#
sub get_group_width {
    my ($remaining) = @_;
    if ($remaining >= 4) {
        return 2*$h_pole_length + $coupler_width;
    } elsif ($remaining == 3) {
        return $h_pole_length + $coupler_width + $h_pole_short_length;
    } elsif ($remaining == 2) {
        return $h_pole_length;
    } elsif ($remaining == 1) {
        return $h_pole_short_length;
    }
    return 0;
}

# The total width is determined by the PVC frame.
# There is a tee every 4 letters, a long pipe for every 2 letters,
# two pipes joined by coupler for every 4 letters.  There is a short
# pipe for the leftover letter when there's an odd number.
#
sub get_width {
    my ($message) = @_;
    my $length = length($message);
    my $width = $tee_width;
    for (my $i = 0; $i < $length; $i += 4) {
        $width += get_group_width($length - $i);
        $width += $tee_width;
    }
    return $width;
}

# a single letter board at the specified position.
# Return width for tracking position.
#
sub letter_board {
    my ($x, $y, $char, $is_blackboard) = @_;
    my $class = $is_blackboard ? "blackboard" : "whiteboard";
    my $board_width = $is_blackboard ? $blackboard_width : $whiteboard_width;
    my $board_height = $is_blackboard ? $blackboard_height : $whiteboard_height;
    $SVG .= "<g transform='translate($x,$y)'>\n";
    $SVG .= "  <rect class='$class' x='0' y='0' width='$board_width' height='$board_height' />\n";
    $SVG .= "  <text class='$class' x='" . ($board_width/2) . "' y='" . ($board_height - 10) . "' text-anchor='middle'>$char</text>\n";
    $SVG .= "</g>\n";
    return $board_width;
}

# the letter boards, leaving space for the PVC frame
#
sub letter_boards {
    my ($message, $is_blackboard) = @_;
    my $group_x = $tee_width - ($pvc_joiner_width / 2);
    my $y = $board_v_offset;
    my $length = length($message);
    my $half_board_width = $is_blackboard ? ($blackboard_width / 2) : ($whiteboard_width / 2);
    for (my $i = 0; $i < $length; $i += 4) {
        my $remaining = $length - $i;
        my $group_length = $remaining >= 4 ? 4 : $remaining;
        my $group_width = get_group_width($remaining) + $pvc_joiner_width;
        my $center_x = $group_width / ($group_length * 2);
        for (my $j = 0; $j < $group_length; $j++) {
            my $x = $group_x + $center_x - $half_board_width;
            my $char = substr($message, $i + $j, 1);
            letter_board($x, $y, $char, $is_blackboard);
            $center_x += $group_width / $group_length;
        }
        $group_x += $group_width - $pvc_joiner_width+ $tee_width;
    }
}

# generate SVG to show the message
# on the specified letter boards in PVC frame
#
sub msg2svg {
    my ($message, $is_blackboard) = @_;
    my $height = $top_margin + $tee_pole_height + $bottom_margin;
    my $width = $left_margin + get_width($message) + $right_margin;

    # not .= here...
    $SVG = "<svg width='$width' height='$height'>\n";
    svg_styles();
    margin_wrapper_start($left_margin, $top_margin);
    pvc_frame($message);
    letter_boards($message, $is_blackboard);
    margin_wrapper_end();
    $SVG .= "</svg>\n";
    return $SVG;
}

1;
