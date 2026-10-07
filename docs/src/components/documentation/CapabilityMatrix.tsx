import React from 'react';
import useBaseUrl from '@docusaurus/useBaseUrl';
import styles from './documentation.module.css';

export type CapabilityStatus = 'yes' | 'partial' | 'no' | 'na';

export interface CapabilityCell {
  status: CapabilityStatus;
  note?: number;
}

export type CapabilityCellInput = CapabilityStatus | CapabilityCell;

export interface CapabilityRow {
  feature: string;
  cells: CapabilityCellInput[];
  link?: string;
}

export interface CapabilityGroup {
  title?: string;
  rows: CapabilityRow[];
}

export interface CapabilityClient {
  name: string;
  icon: string;
}

export interface CapabilityMatrixProps {
  id: string;
  clients: CapabilityClient[];
  groups: CapabilityGroup[];
  notes?: Record<number, string>;
  legend?: boolean;
}

const STATUS_LABEL: Record<CapabilityStatus, string> = {
  yes: 'Yes',
  partial: 'Partial',
  no: 'No',
  na: 'N/A',
};

const STATUS_CLASS: Record<CapabilityStatus, string> = {
  yes: styles.capabilityYes,
  partial: styles.capabilityPartial,
  no: styles.capabilityNo,
  na: styles.capabilityNa,
};

function normalizeCell(cell: CapabilityCellInput): CapabilityCell {
  return typeof cell === 'string' ? { status: cell } : cell;
}

export default function CapabilityMatrix({ id, clients, groups, notes, legend = true }: CapabilityMatrixProps) {
  const iconBase = useBaseUrl('/img/icons/');
  const noteIds = notes
    ? Object.keys(notes)
        .map(Number)
        .sort((a, b) => a - b)
    : [];

  return (
    <div className={styles.capabilityMatrixSection} id={id}>
      {legend && (
        <div className={styles.capabilityLegend}>
          <span className={`${styles.capabilityPill} ${styles.capabilityYes}`}>Yes</span>
          <span className={styles.capabilityLegendText}>Supported</span>
          <span className={`${styles.capabilityPill} ${styles.capabilityPartial}`}>Partial</span>
          <span className={styles.capabilityLegendText}>Works, with caveats</span>
          <span className={`${styles.capabilityPill} ${styles.capabilityNo}`}>No</span>
          <span className={styles.capabilityLegendText}>Not supported by Cleanuparr on this client</span>
          <span className={`${styles.capabilityPill} ${styles.capabilityNa}`}>N/A</span>
          <span className={styles.capabilityLegendText}>The client has no such concept</span>
        </div>
      )}

      <p className={styles.capabilityScrollHint}>Swipe sideways to see all clients &rarr;</p>

      <div className={styles.capabilityTableWrapper}>
        <table className={styles.capabilityTable}>
          <thead>
            <tr>
              <th className={styles.capabilityCornerCell}>Feature</th>
              {clients.map((client) => (
                <th key={client.name} className={styles.capabilityClientHeader}>
                  <span className={styles.capabilityClientHeaderInner}>
                    <img
                      src={`${iconBase}${client.icon}-light.svg`}
                      alt=""
                      className={`${styles.capabilityClientIcon} ${styles.appIconLight}`}
                    />
                    <img
                      src={`${iconBase}${client.icon}-dark.svg`}
                      alt=""
                      className={`${styles.capabilityClientIcon} ${styles.appIconDark}`}
                    />
                    <span className={styles.capabilityClientName}>{client.name}</span>
                  </span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {groups.map((group, groupIndex) => (
              <React.Fragment key={group.title ?? groupIndex}>
                {group.title && (
                  <tr>
                    <th colSpan={clients.length + 1} className={styles.capabilityGroupHeader}>
                      <span className={styles.capabilityGroupHeaderLabel}>{group.title}</span>
                    </th>
                  </tr>
                )}
                {group.rows.map((row, rowIndex) => (
                  <tr key={row.feature}>
                    <th className={styles.capabilityFeatureCell}>
                      {row.link ? <a href={row.link}>{row.feature}</a> : row.feature}
                    </th>
                    {row.cells.map((rawCell, cellIndex) => {
                      const cell = normalizeCell(rawCell);
                      const tooltipId = cell.note
                        ? `${id}-tooltip-${groupIndex}-${rowIndex}-${cellIndex}`
                        : undefined;
                      return (
                        <td key={cellIndex} className={styles.capabilityCell}>
                          <span className={styles.capabilityCellContent}>
                            <span className={`${styles.capabilityPill} ${STATUS_CLASS[cell.status]}`}>
                              {STATUS_LABEL[cell.status]}
                            </span>
                            {cell.note && (
                              <span className={styles.capabilityNoteWrap}>
                                <a
                                  href={`#${id}-note-${cell.note}`}
                                  className={styles.capabilityNoteMarker}
                                  aria-describedby={tooltipId}
                                >
                                  {cell.note}
                                </a>
                                <span role="tooltip" id={tooltipId} className={styles.capabilityTooltip}>
                                  {notes?.[cell.note]}
                                </span>
                              </span>
                            )}
                          </span>
                        </td>
                      );
                    })}
                  </tr>
                ))}
              </React.Fragment>
            ))}
          </tbody>
        </table>
      </div>

      {noteIds.length > 0 && (
        <ol className={styles.capabilityNotesList}>
          {noteIds.map((noteId) => (
            <li key={noteId} id={`${id}-note-${noteId}`}>
              {notes![noteId]}
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
