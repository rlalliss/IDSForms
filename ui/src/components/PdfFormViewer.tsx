import React, { forwardRef, useEffect, useImperativeHandle, useRef } from 'react';
import * as pdfjsLib from 'pdfjs-dist/build/pdf';
import { EventBus, PDFLinkService, PDFViewer } from 'pdfjs-dist/web/pdf_viewer.js';
import workerSrc from 'pdfjs-dist/build/pdf.worker.min.js?url';
import 'pdfjs-dist/web/pdf_viewer.css';

(globalThis as any).pdfjsLib = pdfjsLib;
pdfjsLib.GlobalWorkerOptions.workerSrc = workerSrc;

export type PdfFormViewerHandle = {
  save: () => Promise<Uint8Array>;
};

type PdfFormViewerProps = {
  data?: ArrayBuffer | null;
};

const PdfFormViewer = forwardRef<PdfFormViewerHandle, PdfFormViewerProps>(({ data }, ref) => {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const viewerRef = useRef<PDFViewer | null>(null);
  const pdfDocRef = useRef<any>(null);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    container.classList.add('pdfViewer');

    const viewerRoot = document.createElement('div');
    viewerRoot.className = 'pdfViewer';
    Object.assign(viewerRoot.style, {
      position: 'absolute',
      inset: '0',
      overflow: 'auto'
    });
    container.appendChild(viewerRoot);

    const eventBus = new EventBus();
    const linkService = new PDFLinkService({ eventBus });
    const viewer = new PDFViewer({
      container,
      viewer: viewerRoot,
      eventBus,
      linkService,
      renderInteractiveForms: true
    });
    linkService.setViewer(viewer);
    viewerRef.current = viewer;

    return () => {
      viewer.cleanup();
      viewerRef.current = null;
      pdfDocRef.current?.destroy();
      pdfDocRef.current = null;
      viewerRoot.remove();
      container.classList.remove('pdfViewer');
    };
  }, []);

  useEffect(() => {
    const viewer = viewerRef.current;
    if (!viewer || !data) return;

    const loadingTask = pdfjsLib.getDocument({ data: new Uint8Array(data) });
    let canceled = false;
    loadingTask.promise
      .then((pdf) => {
        if (canceled) return;
        pdfDocRef.current?.destroy();
        pdfDocRef.current = pdf;
        viewer.setDocument(pdf);
        viewer.linkService.setDocument(pdf);
        viewer.currentPageNumber = 1;
        viewer.currentScaleValue = 'page-width';
      })
      .catch(() => {
        // ignore
      });

    return () => {
      canceled = true;
      loadingTask.destroy();
    };
  }, [data]);

  useImperativeHandle(ref, () => ({
    async save() {
      if (!pdfDocRef.current) throw new Error('PDF is not ready yet.');
      const bytes = await pdfDocRef.current.saveDocument();
      return bytes;
    }
  }));

  return <div ref={containerRef} className="pdf-viewer-container" />;
});

export default PdfFormViewer;
